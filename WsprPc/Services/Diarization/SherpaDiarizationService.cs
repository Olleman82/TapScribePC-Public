using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WsprPc.Models;

namespace WsprPc.Services.Diarization;

/// <summary>
/// Speaker diarization service using Sherpa-ONNX.
/// Segments audio by speaker and returns time-stamped segments with speaker IDs.
/// 
/// Note: This is a simplified implementation that currently uses a placeholder
/// diarization algorithm. Full Sherpa-ONNX integration requires matching the 
/// specific API version installed.
/// </summary>
public sealed class SherpaDiarizationService : IDisposable
{
    private readonly string _segmentationModelPath;
    private readonly string _embeddingModelPath;
    private bool _initialized;
    private bool _disposed;

    public SherpaDiarizationService(string segmentationModelPath, string embeddingModelPath)
    {
        _segmentationModelPath = segmentationModelPath;
        _embeddingModelPath = embeddingModelPath;
    }

    /// <summary>
    /// Whether the diarization models are loaded and ready.
    /// </summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// Initialize the diarization models. Call this before Diarize.
    /// </summary>
    public void Initialize()
    {
        if (_initialized)
            return;

        // Verify model files exist
        if (!File.Exists(_segmentationModelPath))
            throw new FileNotFoundException("Segmentation model not found", _segmentationModelPath);
        
        if (!File.Exists(_embeddingModelPath))
            throw new FileNotFoundException("Embedding model not found", _embeddingModelPath);

        _initialized = true;
    }

    /// <summary>
    /// Perform speaker diarization on audio data.
    /// Currently uses a simplified energy-based segmentation until 
    /// full Sherpa-ONNX integration is verified.
    /// </summary>
    /// <param name="audio16kMono">Audio samples at 16kHz mono (float, range -1 to 1)</param>
    /// <param name="expectedSpeakers">Optional hint for number of speakers</param>
    /// <param name="progress">Progress callback</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of segments with speaker IDs</returns>
    public Task<List<DiarizationSegment>> DiarizeAsync(
        float[] audio16kMono,
        int? expectedSpeakers = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            
            if (!_initialized)
                throw new InvalidOperationException("Call Initialize() first");

            var segments = new List<DiarizationSegment>();
            
            progress?.Report(10);

            // Simplified segmentation: Split audio into chunks and assign speakers
            // This is a placeholder until full Sherpa-ONNX API integration
            const double chunkDurationSec = 10.0;
            const int sampleRate = 16000;
            int samplesPerChunk = (int)(chunkDurationSec * sampleRate);
            
            int numSpeakers = expectedSpeakers ?? 2;
            int chunkIndex = 0;
            int totalChunks = (int)Math.Ceiling((double)audio16kMono.Length / samplesPerChunk);

            for (int i = 0; i < audio16kMono.Length; i += samplesPerChunk)
            {
                ct.ThrowIfCancellationRequested();
                
                int chunkEnd = Math.Min(i + samplesPerChunk, audio16kMono.Length);
                double startSec = (double)i / sampleRate;
                double endSec = (double)chunkEnd / sampleRate;

                // Simple speaker assignment based on audio energy
                float energy = CalculateEnergy(audio16kMono, i, chunkEnd);
                int speakerId = energy > 0.01f ? (chunkIndex % numSpeakers) : 0;

                // Only add non-silent segments
                if (energy > 0.001f)
                {
                    segments.Add(new DiarizationSegment(
                        SpeakerId: speakerId,
                        Start: TimeSpan.FromSeconds(startSec),
                        End: TimeSpan.FromSeconds(endSec)
                    ));
                }

                chunkIndex++;
                int progressPercent = 10 + (int)(70.0 * chunkIndex / totalChunks);
                progress?.Report(progressPercent);
            }

            // Merge consecutive segments with same speaker
            segments = MergeConsecutiveSegments(segments);

            progress?.Report(100);
            
            return segments;
        }, ct);
    }

    private static float CalculateEnergy(float[] audio, int start, int end)
    {
        float sum = 0;
        for (int i = start; i < end; i++)
        {
            sum += audio[i] * audio[i];
        }
        return sum / (end - start);
    }

    private static List<DiarizationSegment> MergeConsecutiveSegments(List<DiarizationSegment> segments)
    {
        if (segments.Count <= 1)
            return segments;

        var merged = new List<DiarizationSegment>();
        var current = segments[0];

        for (int i = 1; i < segments.Count; i++)
        {
            var next = segments[i];
            if (next.SpeakerId == current.SpeakerId && 
                (next.Start - current.End).TotalSeconds < 1.0)
            {
                // Merge segments
                current = new DiarizationSegment(
                    current.SpeakerId,
                    current.Start,
                    next.End
                );
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }
        merged.Add(current);

        return merged;
    }

    /// <summary>
    /// Convert 16-bit PCM to float array.
    /// </summary>
    public static float[] ConvertPcm16ToFloat(short[] pcm16)
    {
        var floatSamples = new float[pcm16.Length];
        for (int i = 0; i < pcm16.Length; i++)
        {
            floatSamples[i] = pcm16[i] / 32768f;
        }
        return floatSamples;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
    }
}
