using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WsprPc.Models;

namespace WsprPc.Services.Diarization;

/// <summary>
/// Orchestrates the full file transcription workflow:
/// 1. Load audio file
/// 2. Run speaker diarization
/// 3. Transcribe each segment with Whisper
/// 4. Combine results with speaker labels
/// </summary>
public sealed class FileTranscriptionService : IDisposable
{
    private SherpaDiarizationService _diarizer;
    private readonly WhisperNetEngine _whisper;
    private readonly DependencyManager _dependencyManager;
    private readonly string _segModelPath;
    private readonly string _embModelPath;
    private bool _disposed;

    /// <summary>
    /// Enable/disable pitch-based gender protection to prevent cross-gender speaker merges.
    /// When true, speakers detected as male will never be merged with speakers detected as female.
    /// Default: true
    /// </summary>
    public bool EnablePitchProtection { get; set; } = true;

    /// <summary>
    /// Minimum similarity for merging speakers. Lower = more aggressive merging.
    /// Default: 0.40f (Optimized for TitaNet and Swedish voices when target exists).
    /// </summary>
    public float SafeMergeThreshold { get; set; } = 0.40f;

    /// <summary>
    /// Clustering threshold for Sherpa. Higher = fewer clusters.
    /// Default: 0.75f (Balanced)
    /// </summary>
    public float ClusteringThreshold { get; set; } = 0.75f;

    /// <summary>
    /// Minimum total duration for a speaker to be kept (Ghost Cleanup).
    /// Default: 15.0s (aligned with Test 4)
    /// </summary>
    public double MinTotalDurationSeconds { get; set; } = 15.0;

    /// <summary>
    /// Minimum speech duration before Voice Activity is considered speech.
    /// Higher = filter out short noises (clicks, breaths). Default: 0.15f (aligned with Test 4)
    /// </summary>
    public float MinDurationOn { get; set; } = 0.15f;

    /// <summary>
    /// Minimum silence duration before breaking a speech segment.
    /// Lower = more aggressive splitting (better for rapid speaker changes). Default: 0.10f (aligned with Test 4)
    /// </summary>
    public float MinDurationOff { get; set; } = 0.10f;

    /// <summary>
    /// Force a specific number of speaker clusters in Sherpa.
    /// -1 = auto-detect (default), positive value = forced cluster count.
    /// </summary>
    public int ForcedNumClusters { get; set; } = -1;

    public FileTranscriptionService(WhisperNetEngine whisper, string? baseDir = null)
    {
        _whisper = whisper ?? throw new ArgumentNullException(nameof(whisper));
        _dependencyManager = new DependencyManager(baseDir);
        
        // Note: Models are expected in third_party inside baseDir
        string modelsPath = Path.Combine(baseDir ?? AppContext.BaseDirectory, "third_party", "models", "sherpa");
        _segModelPath = Path.Combine(modelsPath, "sherpa-onnx-reverb-diarization-v1", "model.onnx");
        _embModelPath = Path.Combine(modelsPath, "nemo_en_titanet_large.onnx");

        _diarizer = new SherpaDiarizationService(_segModelPath, _embModelPath);
    }

    /// <summary>
    /// Check if diarization models and tools are available.
    /// </summary>
    public bool ModelsReady => _dependencyManager.AllReady;

    /// <summary>
    /// Download diarization models and FFmpeg if missing.
    /// </summary>
    public async Task EnsureModelsAsync(
        IProgress<(int percent, string status)>? progress = null,
        CancellationToken ct = default)
    {
        await _dependencyManager.EnsureDependenciesAsync(progress, ct);
    }

    /// <summary>
    /// Transcribe an audio file with speaker diarization.
    /// </summary>
    /// <param name="filePath">Path to audio file (MP3, WAV, M4A)</param>
    /// <param name="expectedSpeakers">Optional hint for number of speakers</param>
    /// <param name="progress">Progress callback (percent 0-100, status message)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Formatted transcription with speaker labels</returns>
    public async Task<string> TranscribeAsync(
        string filePath,
        int? expectedSpeakers = null,
        int numThreads = 1,
        IProgress<(int percent, string status)>? progress = null,
        CancellationToken ct = default)
    {
        // Set process priority to BelowNormal during heavy processing
        var originalPriority = Process.GetCurrentProcess().PriorityClass;
        try
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;

            // Phase 1: Load audio (0-10%)
            progress?.Report((0, "Laddar och tvättar ljud..."));
            
            // Use FFmpeg for high-quality preprocessing (balanced profile)
            var audioFloat = await AudioFileLoader.LoadNormalizedAsFloatAsync(filePath, _dependencyManager.FfmpegPath, ct);
            var audioPcm16 = await AudioFileLoader.LoadAsync(filePath, ct);
            progress?.Report((10, "Ljudfil laddad och normaliserad"));

            // Phase 2: Always recreate diarizer to pick up new parameters (fixes threshold bug)
            // Dispose old instance and create new one to apply new ForcedNumClusters/Threshold
            if (_diarizer.IsInitialized)
            {
                progress?.Report((11, "Återinitierar talarmodell med nya inställningar..."));
                _diarizer.Dispose();
                _diarizer = new SherpaDiarizationService(_segModelPath, _embModelPath);
            }
            
            progress?.Report((12, "Initierar talarmodell..."));
            _diarizer.ForcedNumClusters = this.ForcedNumClusters;
            _diarizer.ClusteringThreshold = this.ClusteringThreshold;
            _diarizer.MinDurationOn = this.MinDurationOn;
            _diarizer.MinDurationOff = this.MinDurationOff;
            _diarizer.Initialize(numThreads);

            Debug.WriteLine($"Diarizer Params: Threshold={_diarizer.ClusteringThreshold}, NumClusters={_diarizer.ForcedNumClusters}, MinOn={_diarizer.MinDurationOn}, MinOff={_diarizer.MinDurationOff}");

            // Phase 3: Run diarization (10-40%)
            progress?.Report((15, "Identifierar talare..."));
            
            // Note: Sherpa-ONNX process is a black box for progress.
            // We use a fake internal progress to satisfy the user's desire for movement.
            var diarizationTask = _diarizer.DiarizeAsync(
                audioFloat, 
                expectedSpeakers, 
                null, 
                ct);

            // While diarization is running, we fake some progress movement from 15 to 35
            int fakeProgress = 15;
            while (!diarizationTask.IsCompleted)
            {
                if (fakeProgress < 35)
                {
                    fakeProgress++;
                    progress?.Report((fakeProgress, "Identifierar talare..."));
                }
                await Task.WhenAny(diarizationTask, Task.Delay(500, ct));
                ct.ThrowIfCancellationRequested();
            }

            var segments = await diarizationTask;

            // Phase 3.5: Stage 2 "Statistical Fingerprint Merge"
            progress?.Report((38, "Stärker talarprofiler..."));
            using var fingerprintService = new SpeakerFingerprintService(_diarizer.EmbeddingModelPath, numThreads);
            
            // CRITICAL SETTINGS FOR HIGH ACCURACY MERGE
            fingerprintService.EnablePitchProtection = this.EnablePitchProtection; // Use user setting
            fingerprintService.SafeMergeThreshold = 0.40f;   // Aggressive merge allowed if targeting count
            
            // 1. Merge to Target (if requested)
            if (expectedSpeakers.HasValue && expectedSpeakers.Value > 0 && segments.Count > expectedSpeakers.Value)
            {
                segments = fingerprintService.MergeToCount(segments, audioFloat, AudioFileLoader.TargetSampleRate, expectedSpeakers.Value);
            }
            
            // 2. Cleanup Ghosts (User configurable / Default 5s)
            segments = fingerprintService.CleanupGhostSegments(segments, minTotalDurationSeconds: this.MinTotalDurationSeconds);

            // 3. Add padding (150ms) to prevent chopped-off starts/ends
            segments = segments.Select(s => s with {
                Start = s.Start.TotalSeconds > 0.15 ? s.Start - TimeSpan.FromMilliseconds(150) : TimeSpan.Zero,
                End = s.End + TimeSpan.FromMilliseconds(150)
            }).ToList();

            if (segments.Count == 0)
            {
                progress?.Report((100, "Inga talare hittades"));
                return "[Inga talare hittades i inspelningen]";
            }

            // Phase 4: Transcribe each segment (40-95%)
            progress?.Report((40, $"Transkriberar {segments.Count} segment..."));
            
            var transcribedSegments = new List<DiarizationSegment>();
            int segmentIndex = 0;
            
            foreach (var segment in segments)
            {
                ct.ThrowIfCancellationRequested();
                
                int percent = 40 + (int)(55.0 * segmentIndex / segments.Count);
                progress?.Report((percent, $"Transkriberar segment {segmentIndex + 1}/{segments.Count}..."));

                // Extract audio for this segment
                var segmentAudio = AudioFileLoader.ExtractSegment(audioPcm16, segment.Start, segment.End);
                
                if (segmentAudio.Length < 1600) // Less than 0.1 sec at 16kHz
                {
                    segmentIndex++;
                    continue;
                }

                // Transcribe with Whisper
                string text = await _whisper.TranscribeAsync(segmentAudio, AudioFileLoader.TargetSampleRate);
                
                if (!string.IsNullOrWhiteSpace(text))
                {
                    transcribedSegments.Add(segment.WithText(text.Trim()));
                }

                segmentIndex++;
            }

            // Phase 5: Format output (95-100%)
            progress?.Report((95, "Formaterar resultat..."));
            string result = FormatOutput(transcribedSegments);
            
            progress?.Report((100, "Klart!"));
            return result;
        }
        finally
        {
            // Restore original priority
            try { Process.GetCurrentProcess().PriorityClass = originalPriority; } catch { }
        }
    }


    private static string FormatOutput(List<DiarizationSegment> segments)
    {
        if (segments.Count == 0)
            return "[Ingen text kunde transkriberas]";

        var sb = new StringBuilder();
        int? lastSpeaker = null;

        foreach (var segment in segments)
        {
            // Add speaker label when speaker changes
            if (segment.SpeakerId != lastSpeaker)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                
                string timestamp = $"[{segment.Start:mm\\:ss}]";
                sb.AppendLine($"{timestamp} [Talare {segment.SpeakerId + 1}]");
                lastSpeaker = segment.SpeakerId;
            }

            sb.AppendLine(segment.TranscribedText);
        }

        return sb.ToString().TrimEnd();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        
        _diarizer.Dispose();
        _disposed = true;
    }
}
