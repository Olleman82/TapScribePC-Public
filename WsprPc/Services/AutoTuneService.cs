using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using WsprPc.Services.Vad;

namespace WsprPc.Services;

public sealed record AutoTuneProgress(int StepIndex, int StepCount, string Message, int? Threads);

public sealed record AutoTuneCandidateResult(int Threads, long TotalMs, bool Aborted);

public sealed record AutoTuneResult(int OptimalThreads, IReadOnlyList<AutoTuneCandidateResult> Candidates);

public sealed class AutoTuneSettings
{
    public required string AudioPath { get; init; }
    public required string ModelPath { get; init; }
    public required string VadModelPath { get; init; }
    public required string NativeLibraryPath { get; init; }
    public string Language { get; init; } = "sv";
    public int BeamSize { get; init; } = 2;
    public VadChunkerOptions? VadOptions { get; init; }
    public IReadOnlyList<int>? CandidateThreads { get; init; }
    public double EarlyStopRatio { get; init; } = 1.05;
    public int EarlyStopSlackMs { get; init; } = 300;
}

public sealed class AutoTuneService
{
    public async Task<AutoTuneResult?> RunAsync(
        AutoTuneSettings settings,
        IProgress<AutoTuneProgress>? progress,
        CancellationToken token)
    {
        if (!File.Exists(settings.AudioPath) || !File.Exists(settings.ModelPath) || !File.Exists(settings.VadModelPath))
            return null;

        progress?.Report(new AutoTuneProgress(0, 1, "Analyserar testfil…", null));

        var segments = BuildSegments(settings, token);
        if (segments.Count == 0)
            return null;

        var candidates = settings.CandidateThreads?.Distinct().ToList() ?? BuildDefaultCandidates();
        if (candidates.Count == 0)
            return null;

        var results = new List<AutoTuneCandidateResult>();
        long? bestMs = null;
        int bestThreads = candidates[0];

        for (int i = 0; i < candidates.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            int threads = candidates[i];
            progress?.Report(new AutoTuneProgress(i + 1, candidates.Count, $"Testar {threads} trådar…", threads));

            using var engine = new WhisperNetEngine
            {
                ModelPath = settings.ModelPath,
                BeamSize = settings.BeamSize,
                CpuThreads = threads,
                Language = settings.Language,
                NativeLibraryPath = settings.NativeLibraryPath
            };

            long totalMs = 0;
            bool aborted = false;
            for (int s = 0; s < segments.Count; s++)
            {
                token.ThrowIfCancellationRequested();
                var sw = Stopwatch.StartNew();
                _ = await engine.TranscribeAsync(segments[s], 16000);
                sw.Stop();
                totalMs += sw.ElapsedMilliseconds;

                if (bestMs.HasValue && totalMs > bestMs.Value * settings.EarlyStopRatio + settings.EarlyStopSlackMs)
                {
                    aborted = true;
                    break;
                }
            }

            results.Add(new AutoTuneCandidateResult(threads, totalMs, aborted));
            if (!aborted && (!bestMs.HasValue || totalMs < bestMs.Value))
            {
                bestMs = totalMs;
                bestThreads = threads;
            }

            // Total stop: If we are testing fewer threads than our current champion 
            // and it's already performing worse, we can safely assume even fewer threads won't win.
            if (bestMs.HasValue && threads < bestThreads && (aborted || totalMs > bestMs.Value))
            {
                break;
            }
        }

        return new AutoTuneResult(bestThreads, results);
    }

    private static List<int> BuildDefaultCandidates()
    {
        int cores = Environment.ProcessorCount;
        int max = Math.Max(1, cores);
        int likely = Math.Max(1, cores - 2);
        if (likely % 2 == 1 && likely > 1)
            likely -= 1;

        var list = new List<int> { likely };
        if (max != likely) list.Add(max);
        list.AddRange(new[] { 12, 10, 8, 6, 4, 2, 1 });

        return list.Where(t => t > 0 && t <= max).Distinct().ToList();
    }

    private static List<short[]> BuildSegments(AutoTuneSettings settings, CancellationToken token)
    {
        var segments = new List<short[]>();
        using var reader = new AudioFileReader(settings.AudioPath);
        ISampleProvider provider = reader;
        if (provider.WaveFormat.Channels > 1)
        {
            provider = new StereoToMonoSampleProvider(provider)
            {
                LeftVolume = 0.5f,
                RightVolume = 0.5f
            };
        }
        if (provider.WaveFormat.SampleRate != 16000)
            provider = new WdlResamplingSampleProvider(provider, 16000);

        int sampleRate = provider.WaveFormat.SampleRate;
        var options = settings.VadOptions ?? new VadChunkerOptions();
        using var chunker = new VadChunker(settings.VadModelPath, sampleRate, options);
        chunker.SegmentReady += segment => segments.Add(segment);

        int blockSamples = 800;
        float[] floatBuffer = new float[blockSamples];
        while (true)
        {
            token.ThrowIfCancellationRequested();
            int read = provider.Read(floatBuffer, 0, blockSamples);
            if (read <= 0)
                break;

            short[] pcm = new short[read];
            for (int i = 0; i < read; i++)
            {
                float sample = Math.Clamp(floatBuffer[i], -1f, 1f);
                pcm[i] = (short)Math.Round(sample * short.MaxValue);
            }
            chunker.AddSamples(pcm);
        }

        chunker.Flush();
        return segments;
    }
}
