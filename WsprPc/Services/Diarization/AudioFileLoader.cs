using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using NAudio.Wave;
using WsprPc.Services.Diarization;

namespace WsprPc.Services.Diarization;

/// <summary>
/// Loads audio files (MP3, WAV, M4A) and converts to 16kHz mono PCM.
/// </summary>
public static class AudioFileLoader
{
    public const int TargetSampleRate = 16000;
    
    /// <summary>
    /// Load an audio file and convert to 16kHz mono PCM.
    /// Returns short[] samples suitable for Whisper.
    /// </summary>
    public static async Task<short[]> LoadAsync(string filePath, CancellationToken ct = default)
    {
        return await Task.Run(() => Load(filePath), ct);
    }

    /// <summary>
    /// Load an audio file and convert to 16kHz mono float samples.
    /// Returns float[] samples suitable for Sherpa diarization.
    /// </summary>
    public static async Task<float[]> LoadAsFloatAsync(string filePath, CancellationToken ct = default)
    {
        var pcm16 = await LoadAsync(filePath, ct);
        return ConvertPcm16ToFloat(pcm16);
    }

    /// <summary>
    /// Load an audio file with high-quality FFmpeg normalization and convert to 16kHz mono float samples.
    /// Fallback to NAudio if FFmpeg is not available.
    /// </summary>
    public static async Task<float[]> LoadNormalizedAsFloatAsync(string filePath, string? ffmpegPath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
        {
            return await LoadAsFloatAsync(filePath, ct);
        }

        string tempOut = Path.Combine(Path.GetTempPath(), $"norm_{Guid.NewGuid()}.wav");
        try 
        {
            bool success = await CreateNormalizedVersion(filePath, tempOut, ffmpegPath, ct);
            if (!success) return await LoadAsFloatAsync(filePath, ct);

            return await LoadAsFloatAsync(tempOut, ct);
        }
        finally
        {
            try { if (File.Exists(tempOut)) File.Delete(tempOut); } catch { }
        }
    }

    private static async Task<bool> CreateNormalizedVersion(string input, string output, string ffmpegPath, CancellationToken ct)
    {
        // EBU R128 loudness normalization + high-pass 80Hz + low-pass 7800Hz
        string ffmpegArgs = $"-i \"{input}\" -af \"highpass=f=80,lowpass=f=7800,loudnorm=I=-18:LRA=11:TP=-2\" -ar 16000 -ac 1 \"{output}\" -y";
        
        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = ffmpegArgs,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc == null) return false;

            // CRITICAL: Drain stdout/stderr to prevent buffer deadlock
            // FFmpeg writes verbose output to stderr; if buffer fills, it blocks forever
            var stderrTask = proc.StandardError.ReadToEndAsync(ct);
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
            
            await Task.WhenAll(stderrTask, stdoutTask);
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
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

    private static short[] Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Audio file not found", filePath);

        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        using var reader = CreateReader(filePath, ext);
        
        // Check if we need to resample
        bool needsResampling = reader.WaveFormat.SampleRate != TargetSampleRate ||
                               reader.WaveFormat.Channels != 1 ||
                               reader.WaveFormat.BitsPerSample != 16;

        if (needsResampling)
        {
            var targetFormat = new WaveFormat(TargetSampleRate, 16, 1);
            using var resampler = new MediaFoundationResampler(reader, targetFormat);
            resampler.ResamplerQuality = 60; // High quality
            return ReadAllSamples(resampler);
        }
        
        return ReadAllSamples(reader);
    }

    private static WaveStream CreateReader(string filePath, string ext)
    {
        return ext switch
        {
            ".wav" => new WaveFileReader(filePath),
            ".mp3" => new Mp3FileReader(filePath),
            ".m4a" or ".aac" or ".mp4" => new MediaFoundationReader(filePath),
            _ => throw new NotSupportedException($"Audio format '{ext}' is not supported. Use WAV, MP3, or M4A.")
        };
    }

    private static short[] ReadAllSamples(IWaveProvider waveProvider)
    {
        var samples = new System.Collections.Generic.List<short>();
        var buffer = new byte[8192];

        while (true)
        {
            int bytesRead = waveProvider.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0)
                break;

            int samplesInBuffer = bytesRead / 2;
            for (int i = 0; i < samplesInBuffer; i++)
            {
                samples.Add(BitConverter.ToInt16(buffer, i * 2));
            }
        }

        return samples.ToArray();
    }

    /// <summary>
    /// Extract a slice of audio samples for a specific time range.
    /// </summary>
    public static short[] ExtractSegment(short[] fullAudio, TimeSpan start, TimeSpan end)
    {
        int startSample = (int)(start.TotalSeconds * TargetSampleRate);
        int endSample = (int)(end.TotalSeconds * TargetSampleRate);

        startSample = Math.Max(0, startSample);
        endSample = Math.Min(fullAudio.Length, endSample);

        int length = endSample - startSample;
        if (length <= 0)
            return Array.Empty<short>();

        var segment = new short[length];
        Array.Copy(fullAudio, startSample, segment, 0, length);
        return segment;
    }
}
