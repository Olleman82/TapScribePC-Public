using System;
using System.Collections.Generic;
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
    private readonly SherpaDiarizationService _diarizer;
    private readonly WhisperNetEngine _whisper;
    private readonly ModelDownloader _modelDownloader;
    private bool _disposed;

    public FileTranscriptionService(WhisperNetEngine whisper, string? modelsPath = null)
    {
        _whisper = whisper ?? throw new ArgumentNullException(nameof(whisper));
        _modelDownloader = new ModelDownloader(modelsPath);
        _diarizer = new SherpaDiarizationService(
            _modelDownloader.SegmentationModelPath,
            _modelDownloader.EmbeddingModelPath);
    }

    /// <summary>
    /// Check if diarization models are available.
    /// </summary>
    public bool ModelsReady => _modelDownloader.ModelsExist;

    /// <summary>
    /// Download diarization models if missing.
    /// </summary>
    public async Task EnsureModelsAsync(
        IProgress<(int percent, string status)>? progress = null,
        CancellationToken ct = default)
    {
        await _modelDownloader.EnsureModelsAsync(progress, ct);
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
        // Phase 1: Load audio (0-10%)
        progress?.Report((0, "Laddar ljudfil..."));
        var audioFloat = await AudioFileLoader.LoadAsFloatAsync(filePath, ct);
        var audioPcm16 = await AudioFileLoader.LoadAsync(filePath, ct);
        progress?.Report((10, "Ljudfil laddad"));

        // Phase 2: Initialize diarizer if needed
        if (!_diarizer.IsInitialized)
        {
            progress?.Report((12, "Initierar talarmodell..."));
            _diarizer.Initialize(numThreads);
        }

        // Phase 3: Run diarization (10-40%)
        progress?.Report((15, "Identifierar talare..."));
        var diarizationProgress = new Progress<int>(p => 
            progress?.Report((15 + (int)(p * 0.25), "Identifierar talare...")));
        
        var segments = await _diarizer.DiarizeAsync(
            audioFloat, 
            expectedSpeakers, 
            diarizationProgress, 
            ct);

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
