using System.IO;
using NAudio.Wave;

namespace WsprPc.Services;

public sealed class AudioCaptureService : IDisposable
{
    private WaveInEvent? _waveIn;
    private MemoryStream? _buffer;
    private readonly object _lock = new();

    public int SampleRate { get; } = 16000;
    public event Action<short[]>? SamplesAvailable;

    public void Start()
    {
        lock (_lock)
        {
            _buffer = new MemoryStream();
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(SampleRate, 16, 1),
                BufferMilliseconds = 50
            };
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.StartRecording();
        }
    }

    public short[] StopAndGetPcm16()
    {
        lock (_lock)
        {
            if (_waveIn == null || _buffer == null)
                return Array.Empty<short>();

            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.StopRecording();
            _waveIn.Dispose();
            _waveIn = null;

            byte[] data = _buffer.ToArray();
            _buffer.Dispose();
            _buffer = null;

            short[] samples = new short[data.Length / 2];
            Buffer.BlockCopy(data, 0, samples, 0, data.Length);
            return samples;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        short[]? samples = null;
        lock (_lock)
        {
            _buffer?.Write(e.Buffer, 0, e.BytesRecorded);
            if (e.BytesRecorded > 0)
            {
                samples = new short[e.BytesRecorded / 2];
                Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.BytesRecorded);
            }
        }

        if (samples != null)
            SamplesAvailable?.Invoke(samples);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _waveIn?.Dispose();
            _buffer?.Dispose();
            _waveIn = null;
            _buffer = null;
        }
    }
}
