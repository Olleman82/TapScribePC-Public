using System;
using System.Collections.Generic;
using System.Linq;
using SherpaOnnx;
using WsprPc.Models;

namespace WsprPc.Services.Diarization
{
    /// <summary>
    /// Gender classification based on fundamental frequency (pitch)
    /// </summary>
    public enum GenderGroup { Unknown, Male, Female }

    public class SpeakerFingerprintService : IDisposable
    {
        private readonly SpeakerEmbeddingExtractor _extractor;
        private readonly int _embeddingSize;
        
        /// <summary>
        /// Enable/disable pitch-based gender protection to prevent cross-gender merges
        /// </summary>
        public bool EnablePitchProtection { get; set; } = true;

        /// <summary>
        /// Minimum cosine similarity threshold for merging speakers.
        /// Lower values = more aggressive merging. Default: 0.68f
        /// For TitaNet (192-dim) use lower values like 0.55-0.60
        /// </summary>
        public float SafeMergeThreshold { get; set; } = 0.60f;

        public SpeakerFingerprintService(string modelPath, int numThreads = 1)
        {
            var config = new SpeakerEmbeddingExtractorConfig();
            config.Model = modelPath;
            config.NumThreads = numThreads;
            _extractor = new SpeakerEmbeddingExtractor(config);
            _embeddingSize = _extractor.Dim;
        }

        public float[] ComputeEmbedding(float[] samples, int sampleRate)
        {
            using var stream = _extractor.CreateStream();
            stream.AcceptWaveform(sampleRate, samples);
            stream.InputFinished();
            return _extractor.Compute(stream);
        }

        public List<DiarizationSegment> MergeToCount(List<DiarizationSegment> segments, float[] fullAudio, int sampleRate, int targetCount)
        {
            var uniqueSpeakerIds = segments.Select(s => s.SpeakerId).Distinct().ToList();
            if (uniqueSpeakerIds.Count <= targetCount) return segments;

            // 1. Compute duration-weighted average fingerprint for each detected speaker
            var speakerEmbeddings = new Dictionary<int, float[]>();
            
            // Configuration for the merge process
            const float MinSegmentDuration = 0.5f;    // Ignore extremely short segments for fingerprinting
            const int MaxSegmentsToAnalyze = 15;      // specific number of segments to use for averaging
            
            foreach (var id in uniqueSpeakerIds)
            {
                var speakerSegments = segments.Where(s => s.SpeakerId == id).ToList();
                
                // Prioritize longer segments but take enough of them to get a good average
                var qualitySegments = speakerSegments
                    .Where(s => (s.End - s.Start).TotalSeconds >= MinSegmentDuration)
                    .OrderByDescending(s => (s.End - s.Start).TotalSeconds)
                    .Take(MaxSegmentsToAnalyze)
                    .ToList();
                
                // If we don't have enough long segments, take what we have
                if (qualitySegments.Count == 0)
                {
                    qualitySegments = speakerSegments
                        .OrderByDescending(s => (s.End - s.Start).TotalSeconds)
                        .Take(5)
                        .ToList();
                }

                var embeddingsList = new List<(float[] emb, double duration)>();
                foreach (var seg in qualitySegments)
                {
                    int startSample = (int)(seg.Start.TotalSeconds * sampleRate);
                    int endSample = (int)(seg.End.TotalSeconds * sampleRate);
                    
                    // Safety clamp
                    if (startSample < 0) startSample = 0;
                    if (endSample > fullAudio.Length) endSample = fullAudio.Length;
                    
                    int length = endSample - startSample;
                    
                    if (length < 1600) continue; // Skip very short segments (<0.1s) after clamping

                    var buffer = new float[length];
                    Array.Copy(fullAudio, startSample, buffer, 0, length);
                    
                    try 
                    {
                        var emb = ComputeEmbedding(buffer, sampleRate);
                        embeddingsList.Add((emb, (seg.End - seg.Start).TotalSeconds));
                    }
                    catch { /* Ignore failed embedding extractions */ }
                }

                if (embeddingsList.Count > 0)
                {
                    speakerEmbeddings[id] = DurationWeightedAverageEmbeddings(embeddingsList);
                }
            }

            // 2. Compute pitch-based gender for each speaker (if pitch protection enabled)
            var speakerGenders = new Dictionary<int, GenderGroup>();
            if (EnablePitchProtection)
            {
                foreach (var id in speakerEmbeddings.Keys)
                {
                    // Get longest segment for this speaker for best pitch estimation
                    var longestSeg = segments
                        .Where(s => s.SpeakerId == id)
                        .OrderByDescending(s => (s.End - s.Start).TotalSeconds)
                        .FirstOrDefault();
                    
                    if (longestSeg != null)
                    {
                        int startSample = Math.Max(0, (int)(longestSeg.Start.TotalSeconds * sampleRate));
                        int endSample = Math.Min(fullAudio.Length, (int)(longestSeg.End.TotalSeconds * sampleRate));
                        int length = endSample - startSample;
                        
                        if (length > 4800) // At least 0.3s for reliable pitch
                        {
                            var buffer = new float[length];
                            Array.Copy(fullAudio, startSample, buffer, 0, length);
                            float pitch = EstimatePitch(buffer, sampleRate);
                            speakerGenders[id] = ClassifyGender(pitch);
                            Console.WriteLine($"[PITCH] Talare {id}: F0={pitch:F1}Hz -> {speakerGenders[id]}");
                        }
                        else
                        {
                            speakerGenders[id] = GenderGroup.Unknown;
                        }
                    }
                    else
                    {
                        speakerGenders[id] = GenderGroup.Unknown;
                    }
                }
            }

            // 3. Hierarchical Clustering (Merge most similar until target reached, but respect safety thresholds)
            var currentMapping = uniqueSpeakerIds.ToDictionary(id => id, id => id);
            var blockedPairs = new HashSet<(int, int)>(); // Pairs blocked by pitch protection
            
            Console.WriteLine($"[CONFIG] SafeMergeThreshold = {SafeMergeThreshold}");
            while (speakerEmbeddings.Count > targetCount)
            {
                // Find most similar pair that is not blocked
                int bestI = -1, bestJ = -1;
                float maxSim = -1f;

                var keys = speakerEmbeddings.Keys.ToList();
                for (int i = 0; i < keys.Count; i++)
                {
                    for (int j = i + 1; j < keys.Count; j++)
                    {
                        // Skip blocked pairs
                        var pair = (Math.Min(keys[i], keys[j]), Math.Max(keys[i], keys[j]));
                        if (blockedPairs.Contains(pair)) continue;
                        
                        float sim = CosineSimilarity(speakerEmbeddings[keys[i]], speakerEmbeddings[keys[j]]);
                        if (sim > maxSim)
                        {
                            maxSim = sim;
                            bestI = keys[i];
                            bestJ = keys[j];
                        }
                    }
                }

                if (bestI == -1) break;

                Console.WriteLine($"[DIAGNOSTIK] Överväger merge: Talare {bestI} och Talare {bestJ} (Likhet: {maxSim:F3})");

                // Check pitch protection - never merge different genders
                if (EnablePitchProtection && 
                    speakerGenders.TryGetValue(bestI, out var genderI) && 
                    speakerGenders.TryGetValue(bestJ, out var genderJ) &&
                    genderI != GenderGroup.Unknown && 
                    genderJ != GenderGroup.Unknown &&
                    genderI != genderJ)
                {
                    Console.WriteLine($"[PITCH-GUARD] Blockerar merge: Talare {bestI} ({genderI}) ↔ Talare {bestJ} ({genderJ})");
                    var blockedPair = (Math.Min(bestI, bestJ), Math.Max(bestI, bestJ));
                    blockedPairs.Add(blockedPair);
                    continue; // Try next best pair
                }

                if (maxSim < SafeMergeThreshold) 
                {
                    Console.WriteLine($"[DIAGNOSTIK] Avbryter merge: Likheten {maxSim:F3} är lägre än SafeMergeThreshold ({SafeMergeThreshold}).");
                    break;
                }

                // Merge J into I
                int idToKeep = bestI;
                int idToRemove = bestJ;

                // Update mapping
                foreach (var key in currentMapping.Keys.ToList())
                {
                    if (currentMapping[key] == idToRemove)
                        currentMapping[key] = idToKeep;
                }

                // Recalculate average fingerprint
                // Note: We are doing a simple average of the two centroids here. 
                // A full weighted re-calculation would be better but requires keeping the source lists.
                // For now, simple average of centroids is acceptable for the iterative step.
                var newEmb = new float[_embeddingSize];
                for(int k=0; k<_embeddingSize; k++) 
                    newEmb[k] = (speakerEmbeddings[idToKeep][k] + speakerEmbeddings[idToRemove][k]) / 2f;
                
                // Normalize
                float norm = (float)Math.Sqrt(newEmb.Sum(x => x * x));
                if (norm > 0)
                    for(int k=0; k<_embeddingSize; k++) newEmb[k] /= norm;

                speakerEmbeddings[idToKeep] = newEmb;
                
                // Inherit gender: if either speaker has a known gender, keep it
                if (EnablePitchProtection && speakerGenders.ContainsKey(idToRemove))
                {
                    var removedGender = speakerGenders[idToRemove];
                    var keptGender = speakerGenders.GetValueOrDefault(idToKeep, GenderGroup.Unknown);
                    
                    // If one is Unknown and other is known, inherit the known gender
                    if (keptGender == GenderGroup.Unknown && removedGender != GenderGroup.Unknown)
                    {
                        speakerGenders[idToKeep] = removedGender;
                        Console.WriteLine($"[PITCH] Talare {idToKeep} ärver kön {removedGender} från sammanslagen talare {idToRemove}");
                    }
                    // If both have same gender, keep it; if different (shouldn't happen due to guard), log warning
                    else if (keptGender != GenderGroup.Unknown && removedGender != GenderGroup.Unknown && keptGender != removedGender)
                    {
                        Console.WriteLine($"[WARNING] Merge mixed genders! {idToKeep}={keptGender}, {idToRemove}={removedGender}");
                    }
                    
                    speakerGenders.Remove(idToRemove);
                }
                speakerEmbeddings.Remove(idToRemove);
            }

            // 3. Apply mapping and merge contiguous segments
            var mergedSegments = new List<DiarizationSegment>();
            foreach (var seg in segments)
            {
                int mappedId = currentMapping[seg.SpeakerId];
                if (mergedSegments.Count > 0 && mergedSegments.Last().SpeakerId == mappedId)
                {
                    var last = mergedSegments.Last();
                    mergedSegments[mergedSegments.Count - 1] = last with { End = seg.End };
                }
                else
                {
                    mergedSegments.Add(seg with { SpeakerId = mappedId });
                }
            }

            // 4. Canonicalize IDs to 1, 2, 3... in order of appearance
            var finalResult = new List<DiarizationSegment>();
            var idMapping = new Dictionary<int, int>();
            int nextId = 1;

            foreach (var seg in mergedSegments)
            {
                if (!idMapping.ContainsKey(seg.SpeakerId))
                {
                    idMapping[seg.SpeakerId] = nextId++;
                }
                finalResult.Add(seg with { SpeakerId = idMapping[seg.SpeakerId] });
            }

            return finalResult;
        }

        private float[] DurationWeightedAverageEmbeddings(List<(float[] emb, double duration)> embeddings)
        {
            var avg = new float[_embeddingSize];
            double totalDuration = embeddings.Sum(x => x.duration);
            
            foreach (var item in embeddings)
            {
                float weight = (float)(item.duration / totalDuration);
                for (int i = 0; i < _embeddingSize; i++) 
                    avg[i] += item.emb[i] * weight;
            }
            
            // Re-normalize to unit length
            float norm = (float)Math.Sqrt(avg.Sum(x => x * x));
            if (norm > 0)
            {
                for (int i = 0; i < _embeddingSize; i++) avg[i] /= norm;
            }
            return avg;
        }

        private float CosineSimilarity(float[] a, float[] b)
        {
            float dot = 0;
            for (int i = 0; i < _embeddingSize; i++) dot += a[i] * b[i];
            return dot; // Assumes normalized
        }

        /// <summary>
        /// Estimate fundamental frequency (F0) using autocorrelation method.
        /// Returns 0 if pitch cannot be reliably detected.
        /// </summary>
        private float EstimatePitch(float[] samples, int sampleRate)
        {
            // Pitch range: 50-500 Hz covers deep male to child voices
            int minLag = sampleRate / 500; // 500 Hz max
            int maxLag = sampleRate / 50;  // 50 Hz min
            
            // Use only a portion of the audio for efficiency (first ~0.5s)
            int analyzeLength = Math.Min(samples.Length, sampleRate / 2);
            
            float maxCorr = 0;
            int bestLag = 0;
            
            // Compute autocorrelation
            for (int lag = minLag; lag < maxLag && lag < analyzeLength / 2; lag++)
            {
                float corr = 0;
                float normA = 0, normB = 0;
                
                for (int i = 0; i < analyzeLength - lag; i++)
                {
                    corr += samples[i] * samples[i + lag];
                    normA += samples[i] * samples[i];
                    normB += samples[i + lag] * samples[i + lag];
                }
                
                // Normalize correlation
                float norm = (float)Math.Sqrt(normA * normB);
                if (norm > 0)
                    corr /= norm;
                
                if (corr > maxCorr)
                {
                    maxCorr = corr;
                    bestLag = lag;
                }
            }
            
            // Require minimum correlation strength for reliable detection
            if (maxCorr < 0.3f || bestLag == 0)
                return 0;
            
            return (float)sampleRate / bestLag;
        }

        /// <summary>
        /// Classify gender based on fundamental frequency.
        /// Male typically 85-165 Hz, Female typically 165-255 Hz.
        /// </summary>
        private GenderGroup ClassifyGender(float pitch)
        {
            if (pitch <= 0) return GenderGroup.Unknown;
            if (pitch < 165) return GenderGroup.Male;    
            return GenderGroup.Female;
        }

        /// <summary>
        /// Merges short "ghost" segments (spurious speakers) into the nearest dominant neighbor.
        /// Useful for cleaning up diarization output where key speakers are fragmented.
        /// </summary>
        public List<DiarizationSegment> CleanupGhostSegments(List<DiarizationSegment> segments, double minTotalDurationSeconds = 15.0)
        {
            if (segments == null || segments.Count == 0) return new List<DiarizationSegment>();

            // 1. Identify dominant speakers (Total duration > threshold)
            var durations = segments
                .GroupBy(s => s.SpeakerId)
                .ToDictionary(g => g.Key, g => g.Sum(s => (s.End - s.Start).TotalSeconds));
            
            var dominantSpeakers = durations.Keys.Where(id => durations[id] >= minTotalDurationSeconds).ToHashSet();
            
            // If NO dominant speakers exist, we can't do ghost cleanup
            if (dominantSpeakers.Count == 0) 
                return segments;

            var cleaned = new List<DiarizationSegment>();
            var ghosts = new List<int>();

            // 2. Map ghosts to nearest dominant
            // We do this by iterating and re-assigning IDs
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                if (dominantSpeakers.Contains(seg.SpeakerId))
                {
                    cleaned.Add(seg);
                }
                else
                {
                    // It's a ghost. Find nearest dominant neighbor.
                    // Search backwards
                    DiarizationSegment? prevDom = null;
                    double distPrev = double.MaxValue;
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (dominantSpeakers.Contains(segments[j].SpeakerId))
                        {
                            prevDom = segments[j];
                            distPrev = (seg.Start - segments[j].End).TotalSeconds;
                            break;
                        }
                    }

                    // Search forwards
                    DiarizationSegment? nextDom = null;
                    double distNext = double.MaxValue;
                    for (int j = i + 1; j < segments.Count; j++)
                    {
                        if (dominantSpeakers.Contains(segments[j].SpeakerId))
                        {
                            nextDom = segments[j];
                            distNext = (segments[j].Start - seg.End).TotalSeconds;
                            break;
                        }
                    }

                    int newId = seg.SpeakerId; // Default to self if no neighbors (rare)
                    if (prevDom != null && nextDom != null)
                    {
                        newId = (distPrev <= distNext) ? prevDom.SpeakerId : nextDom.SpeakerId;
                    }
                    else if (prevDom != null)
                    {
                        newId = prevDom.SpeakerId;
                    }
                    else if (nextDom != null)
                    {
                        newId = nextDom.SpeakerId;
                    }

                    // Merge with previous if same ID, or add as new segment with new ID
                    if (cleaned.Count > 0 && cleaned.Last().SpeakerId == newId)
                    {
                        var last = cleaned.Last();
                        // Only merge if they are reasonably close? For ghost cleanup, we usually want to just assign ID.
                        // But let's keep the time gap. So we just add it with the new ID.
                        // Actually, if we just add it, the final result might have fragmented same-speaker segments.
                        // Let's add it, and then run a final "Merge Contiguous" pass.
                        cleaned.Add(seg with { SpeakerId = newId });
                    }
                    else
                    {
                        cleaned.Add(seg with { SpeakerId = newId });
                    }
                }
            }

            // 3. Final Merge of Contiguous Segments with same ID
            var finalMerged = new List<DiarizationSegment>();
            foreach (var seg in cleaned)
            {
                if (finalMerged.Count > 0 && finalMerged.Last().SpeakerId == seg.SpeakerId)
                {
                    // Merge if gap is small? Or always?
                    // "Ghost cleaning" often implies filling the gap. 
                    // But if the gap was real silence, we should perhaps keep it as two segments.
                    // For now, let's keep them separate if there is a gap, but just change ID.
                    // Wait, typical diarization output merges if gap < MinDurationOff. 
                    // Let's merge if the gap is small (< 1.0s), otherwise keep separate.
                    var last = finalMerged.Last();
                    if ((seg.Start - last.End).TotalSeconds < 1.5)
                    {
                        finalMerged[finalMerged.Count - 1] = last with { End = seg.End };
                    }
                    else
                    {
                        finalMerged.Add(seg);
                    }
                }
                else
                {
                    finalMerged.Add(seg);
                }
            }

            return finalMerged;
        }

        public void Dispose()
        {
            _extractor.Dispose();
        }
    }
}
