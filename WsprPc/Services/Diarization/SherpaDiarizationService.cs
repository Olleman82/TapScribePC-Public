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

    public SherpaDiarizationService(string segmentationModelPath, string embeddingModelPath)
    {
        _segmentationModelPath = segmentationModelPath;
        _embeddingModelPath = embeddingModelPath;
    }

    public bool IsInitialized => _diarizer != null;

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
            config.Clustering.NumClusters = -1; // Auto identify number of speakers
            config.Clustering.Threshold = 0.5f; // Threshold for clustering
            config.MinDurationOn = 0.2f;
            config.MinDurationOff = 0.5f;

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

            // If user specified exact speaker count, we must re-initialize the diarizer with new config
            // since NumClusters is part of the initial configuration.
            if (expectedSpeakers.HasValue && expectedSpeakers.Value > 0)
            {
                var config = new OfflineSpeakerDiarizationConfig();
                config.Segmentation.Pyannote.Model = _segmentationModelPath;
                config.Segmentation.NumThreads = _numThreads;
                config.Embedding.Model = _embeddingModelPath;
                config.Embedding.NumThreads = _numThreads;
                config.Clustering.NumClusters = expectedSpeakers.Value;
                config.Clustering.Threshold = 0.5f; 
                config.MinDurationOn = 0.2f;
                config.MinDurationOff = 0.5f;

                // We don't want to leak the old one if we re-init
                _diarizer?.Dispose();
                _diarizer = new OfflineSpeakerDiarization(config);
            }

            progress?.Report(10);

            // Process audio
            // Sherpa-Onnx C# API Process(float[]) returns OfflineSpeakerDiarizationSegment[]
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
                     // seg has .Start, .End, .Speaker
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

        _diarizer?.Dispose();
        _diarizer = null;
        _disposed = true;
    }
}
