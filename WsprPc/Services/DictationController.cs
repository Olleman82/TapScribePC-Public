using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace WsprPc.Services;

public sealed class DictationController : IDisposable
{
    private readonly AudioCaptureService _audio;
    private readonly ITranscriptionEngine _engine;
    private readonly TextPolisher _polisher;
    private readonly PasteInjector _paster;
    private readonly object _segmentLock = new();
    private readonly Dictionary<int, string> _segmentResults = new();
    private readonly Dictionary<int, DateTimeOffset> _segmentQueuedUtc = new();
    private readonly List<Task> _segmentTasks = new();
    private readonly System.Text.StringBuilder _sessionText = new();
    private string? _lastSegmentTail;
    private IAudioChunker? _chunker;
    private Vad.VadChunker? _vadChunker;
    private SemaphoreSlim? _segmentSemaphore;
    private CancellationTokenSource? _segmentCts;
    private int _segmentIndex;
    private int _nextAppendIndex;
    private bool _hasEmittedFirstResult;
    private DateTimeOffset _recordingStartedUtc;
    private Exception? _segmentError;
    private bool _isTranscribing;
    public bool IsTranscribing => _isTranscribing;

    public bool IsRecording { get; private set; }
    public IntPtr TargetWindow { get; set; }
    public bool LastPasteSucceeded { get; private set; } = true;
    public bool EnableSilenceChunking { get; set; } = true;
    public int MaxSegmentWorkers { get; set; } = 1;
    public bool UseModelVad { get; set; } = true;
    public string? VadModelPath { get; set; }
    public float VadSpeechThreshold { get; set; } = 0.05f;
    public int VadMinSpeechMs { get; set; } = 300;
    public int VadMinSilenceMs { get; set; } = 900;
    public int VadSpeechPadMs { get; set; } = 700;
    public double MaxSegmentSeconds { get; set; } = 20.0;
    public double SoftMaxGraceSeconds { get; set; } = 0.2;
    public double OverlapSeconds { get; set; } = 0.0;
    public event Action<string>? PartialResultUpdated;
    public event Action<TimeSpan>? FirstResultReady;
    public event Action<Exception>? SegmentError;
    public event Action<string>? ChunkerDiagnostics;

    public DictationController(
        AudioCaptureService audio,
        ITranscriptionEngine engine,
        TextPolisher polisher,
        PasteInjector paster)
    {
        _audio = audio;
        _engine = engine;
        _polisher = polisher;
        _paster = paster;
    }

    public void StartRecording()
    {
        if (IsRecording || _isTranscribing)
            return;

        IsRecording = true;
        _segmentCts = new CancellationTokenSource();
        _segmentSemaphore = new SemaphoreSlim(Math.Max(1, MaxSegmentWorkers));
        _segmentIndex = 0;
        _nextAppendIndex = 0;
        _segmentResults.Clear();
        _segmentQueuedUtc.Clear();
        _segmentTasks.Clear();
        _sessionText.Clear();
        _segmentError = null;
        _hasEmittedFirstResult = false;
        _recordingStartedUtc = DateTimeOffset.UtcNow;
        _lastSegmentTail = null;

        if (EnableSilenceChunking)
        {
            _chunker = CreateChunker();
            _chunker.SegmentReady += OnSegmentReady;
            _audio.SamplesAvailable += OnSamplesAvailable;
            ChunkerDiagnostics?.Invoke($"Chunker start type={_chunker.GetType().Name} useModelVad={UseModelVad} maxSegment={MaxSegmentSeconds}s");
        }
        else
        {
            ChunkerDiagnostics?.Invoke("Chunker disabled (EnableSilenceChunking=false)");
        }

        try
        {
            _audio.Start();
        }
        catch (Exception ex)
        {
            int win32 = Marshal.GetLastWin32Error();
            ChunkerDiagnostics?.Invoke($"Audio capture start failed: {ex.GetType().Name} {ex.Message} (Win32Error={win32})");
            throw;
        }
    }

    public async Task<string?> StopAndTranscribeAsync()
    {
        return await StopAndTranscribeAsync(true);
    }

    public async Task<string?> StopAndTranscribeAsync(bool pasteResult)
    {
        if (!IsRecording)
            return null;

        IsRecording = false;
        _isTranscribing = true;

        try
        {
            _audio.SamplesAvailable -= OnSamplesAvailable;

            short[] pcm16 = _audio.StopAndGetPcm16();
            if (pcm16.Length > 0)
            {
                double seconds = pcm16.Length / (double)_audio.SampleRate;
                ChunkerDiagnostics?.Invoke($"Captured audio: {pcm16.Length} samples ({seconds:0.00}s)");
            }
            else
            {
                ChunkerDiagnostics?.Invoke("Captured audio: 0 samples");
            }

            if (_chunker == null)
                ChunkerDiagnostics?.Invoke("Chunker stop: no chunker (null)");
            else
                ChunkerDiagnostics?.Invoke($"Chunker stop: flushing type={_chunker.GetType().Name}");

            _chunker?.Flush();
            if (_chunker != null)
                _chunker.SegmentReady -= OnSegmentReady;

            _vadChunker?.Dispose();
            _vadChunker = null;
            _chunker = null;

            if (!_engine.IsReady)
                throw new InvalidOperationException("Transcription engine is not configured.");

            await WaitForSegmentsAsync();

            if (_segmentError != null)
                throw new InvalidOperationException(_segmentError.Message, _segmentError);

            string polished = _sessionText.ToString().Trim();
            if (string.IsNullOrWhiteSpace(polished) && pcm16.Length > 0)
            {
                string raw = await _engine.TranscribeAsync(pcm16, _audio.SampleRate);
                polished = _polisher.Polish(raw);
            }

            if (string.IsNullOrWhiteSpace(polished))
                return null;

            if (pasteResult && !string.IsNullOrWhiteSpace(polished))
            {
                LastPasteSucceeded = _paster.PasteText(polished, TargetWindow);
            }
            else
            {
                LastPasteSucceeded = true;
            }

            return polished;
        }
        finally
        {
            _isTranscribing = false;
        }
    }

    private void OnSamplesAvailable(short[] samples)
    {
        if (_chunker == null)
            return;

        try
        {
            _chunker.AddSamples(samples);
        }
        catch (Exception ex)
        {
            ChunkerDiagnostics?.Invoke($"Chunker error: {ex.GetType().Name} {ex.Message}");
        }
    }

    private IAudioChunker CreateChunker()
    {
        if (!UseModelVad)
            throw new InvalidOperationException("VAD must be enabled.");

        string modelPath = ResolveVadModelPath();
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"VAD model missing at {modelPath}");

        var options = new Vad.VadChunkerOptions
        {
            SpeechThreshold = VadSpeechThreshold,
            MinSpeechMs = VadMinSpeechMs,
            MinSilenceMs = VadMinSilenceMs,
            SpeechPadMs = VadSpeechPadMs,
            MaxSegmentSeconds = MaxSegmentSeconds,
            SoftMaxGraceSeconds = SoftMaxGraceSeconds,
            OverlapSeconds = OverlapSeconds
        };
        _vadChunker = new Vad.VadChunker(modelPath, _audio.SampleRate, options);
        _vadChunker.SegmentEmitted += info =>
        {
            ChunkerDiagnostics?.Invoke(
                $"VAD segment ({info.Reason}) len={info.SegmentMs:0}ms speech={info.SpeechMs:0}ms silence={info.SilenceMs:0}ms");
        };
        return _vadChunker;
    }

    private string ResolveVadModelPath()
    {
        if (!string.IsNullOrWhiteSpace(VadModelPath))
            return VadModelPath;

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDir, "silero_vad.onnx");
    }

    private void OnSegmentReady(short[] segment)
    {
        int index = _segmentIndex++;
        var token = _segmentCts?.Token ?? CancellationToken.None;
        lock (_segmentLock)
        {
            _segmentQueuedUtc[index] = DateTimeOffset.UtcNow;
            _segmentTasks.Add(TranscribeSegmentAsync(index, segment, token));
        }
        ChunkerDiagnostics?.Invoke($"Segment queued idx={index} samples={segment.Length}");
    }

    private async Task TranscribeSegmentAsync(int index, short[] segment, CancellationToken token)
    {
        if (_segmentSemaphore == null)
            return;

        await _segmentSemaphore.WaitAsync(token);
        try
        {
            double queueMs = 0;
            lock (_segmentLock)
            {
                if (_segmentQueuedUtc.TryGetValue(index, out var queuedUtc))
                {
                    queueMs = (DateTimeOffset.UtcNow - queuedUtc).TotalMilliseconds;
                    _segmentQueuedUtc.Remove(index);
                }
            }
            ChunkerDiagnostics?.Invoke($"Segment start idx={index} queueMs={queueMs:0} samples={segment.Length}");
            var swTotal = Stopwatch.StartNew();
            
            // Set initial prompt from previous segment for continuity
            string? prompt = _lastSegmentTail;
            ChunkerDiagnostics?.Invoke($"Segment start idx={index} queueMs={queueMs:0} prompt={(_lastSegmentTail != null ? "set" : "null")}");

            var swTranscribe = Stopwatch.StartNew();
            string raw = await _engine.TranscribeAsync(segment, _audio.SampleRate, prompt);
            swTranscribe.Stop();
            swTotal.Stop();
            string polished = _polisher.Polish(raw);

            // Store tail of this segment for next chunk's context (last ~7 words)
            if (!string.IsNullOrWhiteSpace(polished))
            {
                var words = polished.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 0)
                {
                    int take = Math.Min(words.Length, 7);
                    string tail = string.Join(" ", words[^take..]);
                    
                    // Trim trailing punctuation to avoid Whisper thinking it's the end of a thought
                    _lastSegmentTail = tail.TrimEnd('.', ',', '!', '?', ';', ':');
                    
                    ChunkerDiagnostics?.Invoke($"Segment idx={index} stored tail: '{_lastSegmentTail}'");
                }
            }

            ChunkerDiagnostics?.Invoke($"Segment done idx={index} transcribeMs={swTranscribe.ElapsedMilliseconds} totalMs={swTotal.ElapsedMilliseconds} chars={polished.Length}");
            StoreSegmentResult(index, polished);
        }
        catch (Exception ex)
        {
            _segmentError = ex;
            SegmentError?.Invoke(ex);
        }
        finally
        {
            _segmentSemaphore.Release();
        }
    }

    private void StoreSegmentResult(int index, string result)
    {
        string? toReport = null;
        TimeSpan? firstResultDuration = null;

        lock (_segmentLock)
        {
            _segmentResults[index] = result;
            while (_segmentResults.TryGetValue(_nextAppendIndex, out var next))
            {
                if (!string.IsNullOrWhiteSpace(next))
                {
                    if (_sessionText.Length > 0 && !char.IsWhiteSpace(_sessionText[_sessionText.Length - 1]))
                        _sessionText.Append(' ');
                    _sessionText.Append(next.Trim());
                    toReport = _sessionText.ToString();

                    if (!_hasEmittedFirstResult)
                    {
                        _hasEmittedFirstResult = true;
                        firstResultDuration = DateTimeOffset.UtcNow - _recordingStartedUtc;
                    }
                }

                _segmentResults.Remove(_nextAppendIndex);
                _nextAppendIndex++;
            }
        }

        if (toReport != null)
            PartialResultUpdated?.Invoke(toReport);
        if (firstResultDuration.HasValue)
            FirstResultReady?.Invoke(firstResultDuration.Value);
    }

    private async Task WaitForSegmentsAsync()
    {
        Task[] tasks;
        lock (_segmentLock)
        {
            tasks = _segmentTasks.ToArray();
        }

        if (tasks.Length == 0)
            return;

        await Task.WhenAll(tasks);
        _segmentCts?.Dispose();
        _segmentCts = null;
    }

    public void PasteResult(string text)
    {
        LastPasteSucceeded = _paster.PasteText(text, TargetWindow);
    }

    public void Dispose()
    {
        _segmentCts?.Cancel();
        _audio.Dispose();
    }
}
