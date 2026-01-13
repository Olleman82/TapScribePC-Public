using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

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
        return SherpaDiarizationService.ConvertPcm16ToFloat(pcm16);
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
