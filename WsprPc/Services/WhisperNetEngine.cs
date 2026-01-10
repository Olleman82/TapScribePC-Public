using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Whisper.net;

namespace WsprPc.Services;

public sealed class WhisperNetEngine : ITranscriptionEngine, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WhisperFactory? _factory;
    private string? _loadedModelPath;
    private WhisperProcessor? _processor;
    private ProcessorConfig? _processorConfig;

    public string? ModelPath { get; set; }
    public int CpuThreads { get; set; } = 4;
    public int BeamSize { get; set; } = 2;
    public string Language { get; set; } = "sv";
    public string? NativeLibraryPath { get; set; }

    public bool IsReady =>
        !string.IsNullOrWhiteSpace(ModelPath) &&
        File.Exists(ModelPath);

    public async Task<string> TranscribeAsync(short[] pcm16, int sampleRate, string? initialPrompt = null)
    {
        if (!IsReady)
            throw new InvalidOperationException("Whisper model path is missing.");

        if (pcm16.Length == 0)
            return string.Empty;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var processor = GetProcessor(initialPrompt);
            using var stream = BuildWavStream(pcm16, sampleRate);
            stream.Position = 0;

            var sb = new StringBuilder();
            await foreach (var result in processor.ProcessAsync(stream).ConfigureAwait(false))
            {
                string text = result.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (sb.Length > 0 && !char.IsWhiteSpace(sb[^1]))
                    sb.Append(' ');
                sb.Append(text);
            }

            return sb.ToString().Trim();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _processor?.Dispose();
        _factory?.Dispose();
        _gate.Dispose();
    }

    private WhisperProcessor GetProcessor(string? prompt)
    {
        var factory = GetFactory();
        var config = new ProcessorConfig(
            Language,
            Math.Max(1, CpuThreads),
            Math.Max(1, BeamSize),
            prompt);

        if (_processor != null && _processorConfig != null && _processorConfig.Equals(config))
            return _processor;

        ResetProcessor();

        var builder = factory.CreateBuilder()
            .WithLanguage(config.Language)
            .WithThreads(config.Threads)
            .WithoutSuppressBlank()
            .WithNoSpeechThreshold(1.0f);

        var sampling = builder.WithBeamSearchSamplingStrategy();
        if (sampling is BeamSearchSamplingStrategyBuilder beam)
        {
            beam.WithBeamSize(config.BeamSize);
            builder = beam.ParentBuilder;
        }

        if (!string.IsNullOrWhiteSpace(config.Prompt))
        {
            builder = builder.WithPrompt(config.Prompt);
        }

        _processor = builder.Build();
        _processorConfig = config;
        return _processor;
    }

    private WhisperFactory GetFactory()
    {
        if (string.IsNullOrWhiteSpace(ModelPath))
            throw new InvalidOperationException("Whisper model path is missing.");

        if (_factory == null || !string.Equals(_loadedModelPath, ModelPath, StringComparison.OrdinalIgnoreCase))
        {
            _factory?.Dispose();
            ResetProcessor();
            string? libraryPath = ResolveLibraryPath();
            _factory = WhisperFactory.FromPath(ModelPath, libraryPath: libraryPath);
            _loadedModelPath = ModelPath;
        }

        return _factory;
    }

    private void ResetProcessor()
    {
        _processor?.Dispose();
        _processor = null;
        _processorConfig = null;
    }

    private string? ResolveLibraryPath()
    {
        if (!string.IsNullOrWhiteSpace(NativeLibraryPath) && File.Exists(NativeLibraryPath))
            return NativeLibraryPath;

        string basePath = Path.Combine(AppContext.BaseDirectory, "whisper.dll");
        if (File.Exists(basePath))
            return basePath;

        return null;
    }

    private static MemoryStream BuildWavStream(short[] pcm16, int sampleRate)
    {
        using var stream = new MemoryStream();
        using (var writer = new WaveFileWriter(stream, new WaveFormat(sampleRate, 16, 1)))
        {
            byte[] bytes = new byte[pcm16.Length * 2];
            Buffer.BlockCopy(pcm16, 0, bytes, 0, bytes.Length);
            writer.Write(bytes, 0, bytes.Length);
            writer.Flush();
        }
        return new MemoryStream(stream.ToArray());
    }

    private sealed record ProcessorConfig(string Language, int Threads, int BeamSize, string? Prompt);
}
