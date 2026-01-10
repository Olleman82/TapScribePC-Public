using System;
using System.Collections.Generic;

namespace WsprPc.Services.Vad;

public enum VadChunkerEmitReason
{
    Silence,
    SoftMax,
    Flush
}

public sealed record VadChunkerEmitInfo(
    VadChunkerEmitReason Reason,
    int SegmentSamples,
    int SpeechSamples,
    int SilenceSamples,
    int SampleRate)
{
    public double SegmentMs => SegmentSamples <= 0 ? 0 : SegmentSamples * 1000d / SampleRate;
    public double SpeechMs => SpeechSamples <= 0 ? 0 : SpeechSamples * 1000d / SampleRate;
    public double SilenceMs => SilenceSamples <= 0 ? 0 : SilenceSamples * 1000d / SampleRate;
}

public sealed class VadChunker : IAudioChunker, IDisposable
{
    private readonly SileroVadModel _model;
    private readonly int _sampleRate;
    private readonly int _frameSize;
    private readonly VadChunkerOptions _options;

    private readonly List<short> _pendingSamples = new();
    private readonly List<short[]> _currentBuffers = new();
    private readonly Queue<short[]> _preRollBuffers = new();
    private int _currentSamples;
    private int _speechSamples;
    private int _silenceSamples;
    private int _preRollSamples;
    private bool _forceSplit;
    private int _forceSplitRemaining;

    public event Action<short[]>? SegmentReady;
    public event Action<VadChunkerEmitInfo>? SegmentEmitted;

    public VadChunker(string modelPath, int sampleRate, VadChunkerOptions options)
    {
        _sampleRate = sampleRate;
        _options = options;
        _model = new SileroVadModel(modelPath, sampleRate);
        _frameSize = _model.FrameSize;
    }

    public void AddSamples(short[] samples)
    {
        if (samples.Length == 0)
            return;

        _pendingSamples.AddRange(samples);

        while (_pendingSamples.Count >= _frameSize)
        {
            short[] frame = _pendingSamples.GetRange(0, _frameSize).ToArray();
            _pendingSamples.RemoveRange(0, _frameSize);

            float[] floatFrame = ConvertToFloat(frame);
            float probability = _model.Predict(floatFrame);
            bool isSpeech = probability >= _options.SpeechThreshold;

            ProcessFrame(frame, isSpeech);
        }
    }

    public void Flush()
    {
        if (_pendingSamples.Count > 0)
        {
            short[] remainder = _pendingSamples.ToArray();
            _pendingSamples.Clear();
            ProcessFrame(remainder, isSpeech: true);
        }

        if (_currentBuffers.Count > 0)
            EmitCurrentSegment(VadChunkerEmitReason.Flush);
    }

    public void Reset()
    {
        _pendingSamples.Clear();
        _currentBuffers.Clear();
        _preRollBuffers.Clear();
        _currentSamples = 0;
        _speechSamples = 0;
        _silenceSamples = 0;
        _preRollSamples = 0;
        _forceSplit = false;
        _forceSplitRemaining = 0;
        _model.Reset();
    }

    public void Dispose()
    {
        _model.Dispose();
    }

    private void ProcessFrame(short[] frame, bool isSpeech)
    {
        if (isSpeech)
        {
            if (_currentBuffers.Count == 0 && _preRollBuffers.Count > 0)
            {
                while (_preRollBuffers.Count > 0)
                    _currentBuffers.Add(_preRollBuffers.Dequeue());
                _preRollSamples = 0;
            }

            _currentBuffers.Add(frame);
            _currentSamples += frame.Length;
            _speechSamples += frame.Length;
            _silenceSamples = 0;
        }
        else
        {
            if (_currentBuffers.Count > 0)
            {
                _currentBuffers.Add(frame);
                _currentSamples += frame.Length;
                _silenceSamples += frame.Length;
            }
            else
            {
                _preRollBuffers.Enqueue(frame);
                _preRollSamples += frame.Length;
                TrimPreRoll();
            }
        }

        ApplySoftMaxIfNeeded(isSpeech);

        if (_currentBuffers.Count > 0 && _speechSamples >= SamplesFromMs(_options.MinSpeechMs))
        {
            if (_silenceSamples >= SamplesFromMs(_options.MinSilenceMs))
                EmitCurrentSegment(VadChunkerEmitReason.Silence);
        }
    }

    private void ApplySoftMaxIfNeeded(bool isSpeech)
    {
        int maxSamples = SamplesFromSeconds(_options.MaxSegmentSeconds);
        if (maxSamples <= 0 || _currentSamples < maxSamples)
            return;

        if (!_forceSplit)
        {
            _forceSplit = true;
            _forceSplitRemaining = SamplesFromSeconds(_options.SoftMaxGraceSeconds);
        }

        if (_forceSplitRemaining > 0)
            _forceSplitRemaining -= _frameSize;

        if (_forceSplitRemaining <= 0)
            EmitCurrentSegment(VadChunkerEmitReason.SoftMax);
    }

    private void EmitCurrentSegment(VadChunkerEmitReason reason)
    {
        if (_currentBuffers.Count == 0)
            return;

        int minSpeechSamples = SamplesFromMs(_options.MinSpeechMs);
        if (reason != VadChunkerEmitReason.SoftMax
            && reason != VadChunkerEmitReason.Flush
            && _speechSamples < minSpeechSamples)
        {
            ResetSegmentState();
            return;
        }

        int totalSamples = 0;
        foreach (var buffer in _currentBuffers)
            totalSamples += buffer.Length;

        if (reason != VadChunkerEmitReason.SoftMax && totalSamples == 0)
        {
            ResetSegmentState();
            return;
        }

        var segment = new short[totalSamples];
        int offset = 0;
        foreach (var buffer in _currentBuffers)
        {
            Buffer.BlockCopy(buffer, 0, segment, offset * sizeof(short), buffer.Length * sizeof(short));
            offset += buffer.Length;
        }

        PrepareOverlap(segment);
        SegmentEmitted?.Invoke(new VadChunkerEmitInfo(
            reason,
            totalSamples,
            _speechSamples,
            _silenceSamples,
            _sampleRate));
        SegmentReady?.Invoke(segment);
        ResetSegmentState();
    }

    private void PrepareOverlap(short[] segment)
    {
        int overlapSamples = SamplesFromSeconds(_options.OverlapSeconds);
        if (overlapSamples <= 0)
            return;

        overlapSamples = Math.Min(overlapSamples, segment.Length);
        var tail = new short[overlapSamples];
        Buffer.BlockCopy(segment, (segment.Length - overlapSamples) * sizeof(short), tail, 0, overlapSamples * sizeof(short));
        _preRollBuffers.Enqueue(tail);
        _preRollSamples += tail.Length;
        TrimPreRoll();
    }

    private void ResetSegmentState()
    {
        _currentBuffers.Clear();
        _currentSamples = 0;
        _speechSamples = 0;
        _silenceSamples = 0;
        _forceSplit = false;
        _forceSplitRemaining = 0;
    }

    private void TrimPreRoll()
    {
        int limit = SamplesFromMs(_options.SpeechPadMs);
        while (_preRollSamples > limit && _preRollBuffers.Count > 0)
        {
            var removed = _preRollBuffers.Dequeue();
            _preRollSamples -= removed.Length;
        }
    }

    private int SamplesFromMs(int ms)
    {
        return ms <= 0 ? 0 : (int)Math.Round(_sampleRate * (ms / 1000d));
    }

    private int SamplesFromSeconds(double seconds)
    {
        return seconds <= 0 ? 0 : (int)Math.Round(_sampleRate * seconds);
    }

    private static float[] ConvertToFloat(short[] samples)
    {
        var data = new float[samples.Length];
        for (int i = 0; i < samples.Length; i++)
            data[i] = samples[i] / 32768f;
        return data;
    }
}
