using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SherpaOnnx;
using WsprPc.Models;

namespace WsprPc.Services.Diarization;

/// <summary>
/// Speaker diarization service using Sherpa-ONNX.
/// Segments audio by speaker and returns time-stamped segments with speaker IDs.
/// </summary>
public sealed class SherpaDiarizationService : IDisposable
{
    private readonly string _segmentationModelPath;
    private readonly string _embeddingModelPath;
    private OfflineSpeakerDiarization? _diarizer;
    private int _numThreads = 1;

    private bool _disposed;

    /// <summary>
    /// Force a specific number of speaker clusters.
    /// -1 = auto-detect (default), positive value = forced cluster count.
    /// Must be set BEFORE calling Initialize().
    /// </summary>
    public int ForcedNumClusters { get; set; } = -1;

    public SherpaDiarizationService(string segmentationModelPath, string embeddingModelPath)
    {
        _segmentationModelPath = segmentationModelPath;
        _embeddingModelPath = embeddingModelPath;
    }

    public string EmbeddingModelPath => _embeddingModelPath;

    public bool IsInitialized => _diarizer != null;

    /// <summary>
    /// Clustering threshold (default 0.5). Higher = fewer clusters (more merging).
    /// </summary>
    public float ClusteringThreshold { get; set; } = 0.6f;

    /// <summary>
    /// Ignore speech segments shorter than this (seconds). Default 0.3.
    /// </summary>
    public float MinDurationOn { get; set; } = 0.15f;

    /// <summary>
    /// Merge same-speaker segments with gap shorter than this (seconds). Default 0.5.
    /// Increasing this helps merge fragmented speech.
    /// </summary>
    public float MinDurationOff { get; set; } = 0.1f;

    public void Initialize(int numThreads = 1)
    {
        _numThreads = numThreads;
        if (_diarizer != null)
            return;

        if (!File.Exists(_segmentationModelPath))
            throw new FileNotFoundException("Segmentation model not found", _segmentationModelPath);

        if (!File.Exists(_embeddingModelPath))
            throw new FileNotFoundException("Embedding model not found", _embeddingModelPath);

        try 
        {
            var config = new OfflineSpeakerDiarizationConfig();
            config.Segmentation.Pyannote.Model = _segmentationModelPath;
            config.Segmentation.NumThreads = _numThreads;
            config.Embedding.Model = _embeddingModelPath;
            config.Embedding.NumThreads = _numThreads;
            config.Clustering.NumClusters = ForcedNumClusters; // -1 = auto, positive = forced
            config.Clustering.Threshold = ClusteringThreshold;
            config.MinDurationOn = MinDurationOn;
            config.MinDurationOff = MinDurationOff;
            
            Console.WriteLine($"[SHERPA] Segmentation Model: {_segmentationModelPath}");
            Console.WriteLine($"[SHERPA] Embedding Model: {_embeddingModelPath}");
            Console.WriteLine($"[SHERPA] NumClusters={ForcedNumClusters}, Threshold={ClusteringThreshold:F2}, MinOn={MinDurationOn:F2}, MinOff={MinDurationOff:F2}");

            _diarizer = new OfflineSpeakerDiarization(config);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize Sherpa Diarization: {ex.Message}", ex);
        }
    }

    public Task<List<DiarizationSegment>> DiarizeAsync(
        float[] audio16kMono,
        int? expectedSpeakers = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            if (_diarizer == null)
                throw new InvalidOperationException("Call Initialize() first");

            progress?.Report(10);

            // Process audio directly (Sherpa-ONNX 1.12.22 handles long files correctly)
            Console.WriteLine($"[SHERPA] Processing {audio16kMono.Length / 16000.0f / 60:F1} minutes of audio...");
            
            OfflineSpeakerDiarizationSegment[]? resultLines = null;
            try 
            {
                 resultLines = _diarizer.Process(audio16kMono);
            }
            catch(Exception ex)
            {
                 throw new InvalidOperationException($"Diarization process failed: {ex.Message}", ex);
            }
            
            progress?.Report(90);

            var segments = new List<DiarizationSegment>();
            
            if (resultLines != null)
            {
                foreach (var seg in resultLines)
                {
                     segments.Add(new DiarizationSegment(
                         SpeakerId: seg.Speaker,
                         Start: TimeSpan.FromSeconds(seg.Start),
                         End: TimeSpan.FromSeconds(seg.End)
                     ));
                }
            }

            progress?.Report(100);

            return segments;
        }, ct);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _diarizer?.Dispose();
        _diarizer = null;
        _disposed = true;
    }
}
