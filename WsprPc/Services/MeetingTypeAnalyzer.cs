using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using NAudio.Wave;

namespace WsprPc.Services;

/// <summary>
/// Analyserar ljudfiler för att avgöra om inspelningen är från ett fysiskt eller digitalt möte.
/// Fysiska möten (samma rum) har konsekvent bakgrundsljud, digitala (Teams/Zoom) har varierande.
/// </summary>
public sealed class MeetingTypeAnalyzer
{
    // ============== KONFIGURATION ==============
    private const int WINDOW_SIZE_MS = 50;
    private const int FFT_SIZE = 1024;
    private const double SILENCE_RMS_THRESHOLD = 0.02;
    private const double SPECTRAL_VAR_THRESHOLD = 0.012;
    private const int MIN_CONFIDENCE_SEGMENTS = 100;
    // ===========================================

    public record MeetingAnalysisResult(
        bool IsPhysicalMeeting,
        double SpectralVariance,
        int SilenceSegmentCount,
        bool IsHighConfidence,
        string Description
    );

    /// <summary>
    /// Analyserar en ljudfil för att avgöra mötestyp.
    /// </summary>
    public async Task<MeetingAnalysisResult> AnalyzeAsync(string audioPath)
    {
        return await Task.Run(() => Analyze(audioPath));
    }

    private MeetingAnalysisResult Analyze(string audioPath)
    {
        Console.WriteLine($"[MEETING-TYPE] Starting analysis: {Path.GetFileName(audioPath)}");

        try
        {
            using var reader = new MediaFoundationReader(audioPath);

            var sampleRate = reader.WaveFormat.SampleRate;
            var channels = reader.WaveFormat.Channels;
            var bytesPerSample = reader.WaveFormat.BitsPerSample / 8;
            var totalBytes = reader.Length;
            var totalSeconds = reader.TotalTime.TotalSeconds;

            if (totalSeconds < 10)
            {
                Console.WriteLine("[MEETING-TYPE] Skipping analysis: file too short (< 10s)");
                return new MeetingAnalysisResult(false, 0, 0, false, "Fil för kort");
            }

            var samplesPerWindow = (int)(sampleRate * (WINDOW_SIZE_MS / 1000.0));
            var bytesPerWindow = samplesPerWindow * channels * bytesPerSample;

            // Läs hela filen för att hitta tysta segment
            var allBytes = new byte[totalBytes];
            reader.Position = 0;
            var totalRead = reader.Read(allBytes, 0, (int)totalBytes);

            // Konvertera till mono floats
            var allSamples = ConvertToMonoFloats(allBytes, totalRead, channels, bytesPerSample);

            // Beräkna global RMS
            double globalRms = 0;
            for (int i = 0; i < allSamples.Length; i++)
                globalRms += allSamples[i] * allSamples[i];
            globalRms = Math.Sqrt(globalRms / allSamples.Length);

            if (globalRms < 0.001)
            {
                Console.WriteLine("[MEETING-TYPE] Skipping analysis: file too quiet");
                return new MeetingAnalysisResult(false, 0, 0, false, "Fil för tyst");
            }

            // Identifiera tysta segment
            var silenceSegments = new List<double[]>();

            for (int i = 0; i < allSamples.Length - samplesPerWindow; i += samplesPerWindow)
            {
                double localRms = 0;
                for (int j = 0; j < samplesPerWindow; j++)
                    localRms += allSamples[i + j] * allSamples[i + j];
                localRms = Math.Sqrt(localRms / samplesPerWindow);

                var relativeRms = localRms / globalRms;

                if (relativeRms < SILENCE_RMS_THRESHOLD && relativeRms > 0.001)
                {
                    var windowSamples = new float[samplesPerWindow];
                    Array.Copy(allSamples, i, windowSamples, 0, samplesPerWindow);

                    var spectrum = ComputeNormalizedSpectrum(windowSamples, sampleRate);
                    silenceSegments.Add(spectrum);

                    // Hoppa framåt för att undvika överlappande segment
                    i += samplesPerWindow * 2;
                }
            }

            // Fallback om för få tysta segment
            if (silenceSegments.Count < 20)
            {
                var segments = new List<(int start, double rms)>();
                for (int i = 0; i < allSamples.Length - samplesPerWindow; i += samplesPerWindow)
                {
                    double localRms = 0;
                    for (int j = 0; j < samplesPerWindow; j++)
                        localRms += allSamples[i + j] * allSamples[i + j];
                    localRms = Math.Sqrt(localRms / samplesPerWindow);
                    segments.Add((i, localRms));
                }

                silenceSegments.Clear();
                segments.Sort((a, b) => a.rms.CompareTo(b.rms));
                foreach (var seg in segments.GetRange(0, Math.Min(50, segments.Count)))
                {
                    var windowSamples = new float[samplesPerWindow];
                    Array.Copy(allSamples, seg.start, windowSamples, 0, samplesPerWindow);
                    var spectrum = ComputeNormalizedSpectrum(windowSamples, sampleRate);
                    silenceSegments.Add(spectrum);
                }
            }

            if (silenceSegments.Count < 3)
            {
                Console.WriteLine("[MEETING-TYPE] Skipping analysis: too few silence segments");
                return new MeetingAnalysisResult(false, 0, silenceSegments.Count, false, "För få tysta segment");
            }

            // Beräkna spektral varians
            var spectralVariance = ComputeSpectralVariance(silenceSegments);
            var isHighConfidence = silenceSegments.Count >= MIN_CONFIDENCE_SEGMENTS;
            var isPhysical = spectralVariance < SPECTRAL_VAR_THRESHOLD;

            var typeStr = isPhysical ? "PHYSICAL" : "DIGITAL";
            var confidenceStr = isHighConfidence ? "high confidence" : "LOW CONFIDENCE";

            Console.WriteLine($"[MEETING-TYPE] Found {silenceSegments.Count} silence segments, spectralVar={spectralVariance:F4}");
            Console.WriteLine($"[MEETING-TYPE] Result: {typeStr} ({confidenceStr})");

            var description = isPhysical ? "Fysiskt möte" : "Digitalt möte";
            if (!isHighConfidence) description += " (osäkert)";

            return new MeetingAnalysisResult(
                isPhysical,
                spectralVariance,
                silenceSegments.Count,
                isHighConfidence,
                description
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MEETING-TYPE] Error analyzing file: {ex.Message}");
            return new MeetingAnalysisResult(false, 0, 0, false, $"Fel: {ex.Message}");
        }
    }

    private static float[] ConvertToMonoFloats(byte[] buffer, int bytesRead, int channels, int bytesPerSample)
    {
        int totalSamples = bytesRead / (channels * bytesPerSample);
        var mono = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float sum = 0;
            for (int c = 0; c < channels; c++)
            {
                int byteIndex = (i * channels + c) * bytesPerSample;
                if (byteIndex + bytesPerSample > buffer.Length) break;

                sum += bytesPerSample == 2
                    ? BitConverter.ToInt16(buffer, byteIndex) / 32768f
                    : BitConverter.ToSingle(buffer, byteIndex);
            }
            mono[i] = sum / channels;
        }
        return mono;
    }

    private static double[] ComputeNormalizedSpectrum(float[] samples, int sampleRate)
    {
        var fftBuffer = new Complex[FFT_SIZE];

        for (int i = 0; i < FFT_SIZE; i++)
        {
            if (i < samples.Length)
            {
                var window = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (FFT_SIZE - 1)));
                fftBuffer[i] = new Complex(samples[i] * window, 0);
            }
            else
            {
                fftBuffer[i] = Complex.Zero;
            }
        }

        FFT(fftBuffer);

        var spectrum = new double[FFT_SIZE / 2];
        double total = 0;

        for (int i = 0; i < spectrum.Length; i++)
        {
            spectrum[i] = fftBuffer[i].Magnitude;
            total += spectrum[i];
        }

        if (total > 0.0001)
        {
            for (int i = 0; i < spectrum.Length; i++)
                spectrum[i] /= total;
        }

        return spectrum;
    }

    private static double ComputeSpectralVariance(List<double[]> spectra)
    {
        if (spectra.Count < 2) return 0;

        var len = spectra[0].Length;
        var means = new double[len];

        for (int f = 0; f < len; f++)
        {
            for (int s = 0; s < spectra.Count; s++)
                means[f] += spectra[s][f];
            means[f] /= spectra.Count;
        }

        double totalVar = 0;
        for (int f = 0; f < len; f++)
        {
            double variance = 0;
            for (int s = 0; s < spectra.Count; s++)
            {
                double diff = spectra[s][f] - means[f];
                variance += diff * diff;
            }
            totalVar += variance / spectra.Count;
        }

        return totalVar;
    }

    private static void FFT(Complex[] buffer)
    {
        int n = buffer.Length;
        if (n <= 1) return;

        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = -2 * Math.PI / len;
            var wlen = new Complex(Math.Cos(angle), Math.Sin(angle));

            for (int i = 0; i < n; i += len)
            {
                var w = Complex.One;
                for (int j = 0; j < len / 2; j++)
                {
                    var u = buffer[i + j];
                    var v = buffer[i + j + len / 2] * w;
                    buffer[i + j] = u + v;
                    buffer[i + j + len / 2] = u - v;
                    w *= wlen;
                }
            }
        }
    }
}
