using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;
using WsprPc.Services;
using WsprPc.Services.Diarization;
using WsprPc.Models;

namespace DiarizationBenchmark
{
    class Program
    {
        private static StringBuilder _currentLog = new StringBuilder();
        private static string _outputDir = string.Empty;

        static void Log(string message)
        {
            string timestamped = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            Console.WriteLine(timestamped);
            _currentLog.AppendLine(timestamped);
        }

        static async Task Main(string[] args)
        {
            string projectRoot = @"D:\Appar\wspr-pc";
            var files = new[] { 
                @"C:\OBS\2026-01-16 14-33-50.mp3", 
                @"C:\OBS\2025-12-10_tylenius_intervju.mp3" 
            };

            foreach (var audioPath in files)
            {
                string name = Path.GetFileNameWithoutExtension(audioPath).Contains("tylenius") ? "tylenius" : "lackolle";
                _outputDir = Path.Combine(projectRoot, "scripts", "DiarizationBenchmark", $"output_{name}_v3");
                Directory.CreateDirectory(_outputDir);

                Log("\n" + new string('=', 50));
                Log($"  STARTING BENCHMARK FOR: {name.ToUpper()}");
                Log(new string('=', 50));
                Log($"Source Audio: {audioPath}");
                Log($"Output Dir: {_outputDir}");

                if (!File.Exists(audioPath))
                {
                    Log($"ERROR: Audio file not found at {audioPath}");
                    continue;
                }

                // Extract and Normalize (once per file)
                string audio10Min = Path.Combine(_outputDir, "audio_10min_raw.wav");
                string audio10MinNorm = Path.Combine(_outputDir, "audio_10min_normalized.wav");

                Log("\n--- STEP 1: Preprocessing ---");
                await ExtractFirst10Minutes(audioPath, audio10Min);
                await CreateNormalizedVersion(audio10Min, audio10MinNorm);

                // Setup Models
                string modelsBaseDir = Path.Combine(projectRoot, "third_party", "models", "sherpa");
                var downloader = new ModelDownloader(modelsBaseDir);
                if (!downloader.ModelsExist) await downloader.EnsureModelsAsync();

                // Run the top 3 contenders for each file
                await RunTest4_NormalizedThreshold085Auto(audio10MinNorm, downloader);
                await RunTest5_RefinedBalance(audio10MinNorm, downloader);
                await RunTest6_Sensitive(audio10MinNorm, downloader);
            }

            Log("\n========================================");
            Log("  ALL TESTS COMPLETED");
            Log("========================================");
        }

        static async Task ExtractFirst10Minutes(string input, string output)
        {
            if (File.Exists(output))
            {
                Log($"Using cached file: {output}");
                return;
            }

            var ffmpegArgs = $"-i \"{input}\" -t 600 -ac 1 -ar 16000 \"{output}\" -y";
            Log($"Running: ffmpeg {ffmpegArgs}");
            
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = ffmpegArgs,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi)!;
            string stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            
            if (proc.ExitCode != 0)
                Log($"FFmpeg error: {stderr}");
            else
                Log($"Created: {output} ({new FileInfo(output).Length / 1024}KB)");
        }

        static async Task CreateNormalizedVersion(string input, string output)
        {
            if (File.Exists(output))
            {
                Log($"Using cached normalized file: {output}");
                return;
            }

            // EBU R128 loudness normalization + high-pass 80Hz + low-pass 7800Hz
            // Explicitly set -ar 16000 to avoid upsampling issues
            var ffmpegArgs = $"-i \"{input}\" -af \"highpass=f=80,lowpass=f=7800,loudnorm=I=-18:LRA=11:TP=-2\" -ar 16000 \"{output}\" -y";
            Log($"Running: ffmpeg {ffmpegArgs}");
            
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = ffmpegArgs,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi)!;
            string stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            
            if (proc.ExitCode != 0)
                Log($"FFmpeg error: {stderr}");
            else
                Log($"Created normalized: {output} ({new FileInfo(output).Length / 1024}KB)");
        }

        static async Task RunTest1_CurrentSetup(string audioPath, ModelDownloader downloader)
        {
            _currentLog.Clear();
            Log("\n###############################################");
            Log("### TEST 1: Current Setup (No Norm, Speakers=3)");
            Log("###############################################");
            Log($"Audio: {audioPath}");
            Log("Parameters:");
            Log("  - Preprocessing: NONE");
            Log("  - ClusteringThreshold: 0.60");
            Log("  - MinDurationOn: 0.15");
            Log("  - MinDurationOff: 0.10");
            Log("  - ForcedNumClusters: -1 (auto)");
            Log("  - SafeMergeThreshold: 0.40");
            Log("  - MergeToCount: 3");
            Log("  - PitchProtection: true");

            var sw = Stopwatch.StartNew();
            var audioMono = await AudioFileLoader.LoadAsFloatAsync(audioPath);
            Log($"Audio loaded: {audioMono.Length / 16000.0f / 60:F2} minutes");

            List<DiarizationSegment> segments;
            using (var sherpa = new SherpaDiarizationService(downloader.SegmentationModelPath, downloader.EmbeddingModelPath))
            {
                sherpa.ClusteringThreshold = 0.60f;
                sherpa.MinDurationOn = 0.15f;
                sherpa.MinDurationOff = 0.10f;
                sherpa.ForcedNumClusters = -1; // Auto
                Log($"Initializing Sherpa...");
                sherpa.Initialize(Environment.ProcessorCount);
                
                Log($"Running diarization...");
                segments = await sherpa.DiarizeAsync(audioMono);
                Log($"Sherpa returned {segments.Count} raw segments");
                LogSegmentDistribution(segments, "Raw segments");
            }

            using (var fingerprinter = new SpeakerFingerprintService(downloader.EmbeddingModelPath, Environment.ProcessorCount))
            {
                fingerprinter.EnablePitchProtection = true;
                fingerprinter.SafeMergeThreshold = 0.40f;
                
                Log($"\n--- MergeToCount(target=3) ---");
                var merged = fingerprinter.MergeToCount(segments, audioMono, 16000, 3);
                LogSegmentDistribution(merged, "After MergeToCount");
                
                Log($"\n--- CleanupGhostSegments(minDuration=15s) ---");
                var cleaned = fingerprinter.CleanupGhostSegments(merged, 15.0);
                LogSegmentDistribution(cleaned, "After CleanupGhostSegments");
                
                // Add padding
                var padded = cleaned.Select(s => s with {
                    Start = s.Start.TotalSeconds > 0.15 ? s.Start - TimeSpan.FromMilliseconds(150) : TimeSpan.Zero,
                    End = s.End + TimeSpan.FromMilliseconds(150)
                }).ToList();

                sw.Stop();
                Log($"\nTotal time: {sw.Elapsed.TotalSeconds:F1}s");
                
                SaveResult("Test1_CurrentSetup", padded);
            }
            
            await File.WriteAllTextAsync(Path.Combine(_outputDir, "Test1_CurrentSetup.log"), _currentLog.ToString());
        }

        static async Task RunTest2_NormalizedSpeakers3(string audioPath, ModelDownloader downloader)
        {
            _currentLog.Clear();
            Log("\n###############################################");
            Log("### TEST 2: Normalized + Speakers=3");
            Log("###############################################");
            Log($"Audio: {audioPath}");
            Log("Parameters:");
            Log("  - Preprocessing: highpass=80Hz, lowpass=7800Hz, loudnorm");
            Log("  - ClusteringThreshold: 0.60");
            Log("  - MinDurationOn: 0.15");
            Log("  - MinDurationOff: 0.10");
            Log("  - ForcedNumClusters: -1 (auto)");
            Log("  - SafeMergeThreshold: 0.40");
            Log("  - MergeToCount: 3");
            Log("  - PitchProtection: true");

            var sw = Stopwatch.StartNew();
            var audioMono = await AudioFileLoader.LoadAsFloatAsync(audioPath);
            Log($"Audio loaded: {audioMono.Length / 16000.0f / 60:F2} minutes");

            List<DiarizationSegment> segments;
            using (var sherpa = new SherpaDiarizationService(downloader.SegmentationModelPath, downloader.EmbeddingModelPath))
            {
                sherpa.ClusteringThreshold = 0.60f;
                sherpa.MinDurationOn = 0.15f;
                sherpa.MinDurationOff = 0.10f;
                sherpa.ForcedNumClusters = -1;
                Log($"Initializing Sherpa...");
                sherpa.Initialize(Environment.ProcessorCount);
                
                Log($"Running diarization...");
                segments = await sherpa.DiarizeAsync(audioMono);
                Log($"Sherpa returned {segments.Count} raw segments");
                LogSegmentDistribution(segments, "Raw segments");
            }

            using (var fingerprinter = new SpeakerFingerprintService(downloader.EmbeddingModelPath, Environment.ProcessorCount))
            {
                fingerprinter.EnablePitchProtection = true;
                fingerprinter.SafeMergeThreshold = 0.40f;
                
                Log($"\n--- MergeToCount(target=3) ---");
                var merged = fingerprinter.MergeToCount(segments, audioMono, 16000, 3);
                LogSegmentDistribution(merged, "After MergeToCount");
                
                Log($"\n--- CleanupGhostSegments(minDuration=15s) ---");
                var cleaned = fingerprinter.CleanupGhostSegments(merged, 15.0);
                LogSegmentDistribution(cleaned, "After CleanupGhostSegments");
                
                var padded = cleaned.Select(s => s with {
                    Start = s.Start.TotalSeconds > 0.15 ? s.Start - TimeSpan.FromMilliseconds(150) : TimeSpan.Zero,
                    End = s.End + TimeSpan.FromMilliseconds(150)
                }).ToList();

                sw.Stop();
                Log($"\nTotal time: {sw.Elapsed.TotalSeconds:F1}s");
                
                SaveResult("Test2_NormalizedSpeakers3", padded);
            }
            
            await File.WriteAllTextAsync(Path.Combine(_outputDir, "Test2_NormalizedSpeakers3.log"), _currentLog.ToString());
        }

        static async Task RunTest3_NormalizedAutoSpeakers(string audioPath, ModelDownloader downloader)
        {
            _currentLog.Clear();
            Log("\n###############################################");
            Log("### TEST 3: Normalized + Auto Speakers");
            Log("###############################################");
            Log($"Audio: {audioPath}");
            Log("Parameters:");
            Log("  - Preprocessing: highpass=80Hz, lowpass=7800Hz, loudnorm");
            Log("  - ClusteringThreshold: 0.60");
            Log("  - MinDurationOn: 0.15");
            Log("  - MinDurationOff: 0.10");
            Log("  - ForcedNumClusters: -1 (auto)");
            Log("  - SafeMergeThreshold: 0.40");
            Log("  - MergeToCount: NO (Auto)");
            Log("  - PitchProtection: true");

            var sw = Stopwatch.StartNew();
            var audioMono = await AudioFileLoader.LoadAsFloatAsync(audioPath);
            Log($"Audio loaded: {audioMono.Length / 16000.0f / 60:F2} minutes");

            List<DiarizationSegment> segments;
            using (var sherpa = new SherpaDiarizationService(downloader.SegmentationModelPath, downloader.EmbeddingModelPath))
            {
                sherpa.ClusteringThreshold = 0.60f;
                sherpa.MinDurationOn = 0.15f;
                sherpa.MinDurationOff = 0.10f;
                sherpa.ForcedNumClusters = -1;
                Log($"Initializing Sherpa...");
                sherpa.Initialize(Environment.ProcessorCount);
                
                Log($"Running diarization...");
                segments = await sherpa.DiarizeAsync(audioMono);
                Log($"Sherpa returned {segments.Count} raw segments");
                LogSegmentDistribution(segments, "Raw segments");
            }

            using (var fingerprinter = new SpeakerFingerprintService(downloader.EmbeddingModelPath, Environment.ProcessorCount))
            {
                fingerprinter.EnablePitchProtection = true;
                fingerprinter.SafeMergeThreshold = 0.40f;
                
                // NO MergeToCount - let it be auto
                Log($"\n--- Skipping MergeToCount (Auto mode) ---");
                
                Log($"\n--- CleanupGhostSegments(minDuration=15s) ---");
                var cleaned = fingerprinter.CleanupGhostSegments(segments, 15.0);
                LogSegmentDistribution(cleaned, "After CleanupGhostSegments");
                
                var padded = cleaned.Select(s => s with {
                    Start = s.Start.TotalSeconds > 0.15 ? s.Start - TimeSpan.FromMilliseconds(150) : TimeSpan.Zero,
                    End = s.End + TimeSpan.FromMilliseconds(150)
                }).ToList();

                sw.Stop();
                Log($"\nTotal time: {sw.Elapsed.TotalSeconds:F1}s");
                
                SaveResult("Test3_NormalizedAutoSpeakers", padded);
            }
            
            await File.WriteAllTextAsync(Path.Combine(_outputDir, "Test3_NormalizedAutoSpeakers.log"), _currentLog.ToString());
        }

        static async Task RunTest4_NormalizedThreshold085Auto(string audioPath, ModelDownloader downloader)
        {
            _currentLog.Clear();
            Log("\n###############################################");
            Log("### TEST 4: Normalized + Threshold=0.85 + Auto");
            Log("###############################################");
            Log($"Audio: {audioPath}");
            Log("Parameters:");
            Log("  - Preprocessing: highpass=80Hz, lowpass=7800Hz, loudnorm");
            Log("  - ClusteringThreshold: 0.85 (CHANGED)");
            Log("  - MinDurationOn: 0.15");
            Log("  - MinDurationOff: 0.10");
            Log("  - ForcedNumClusters: -1 (auto)");
            Log("  - SafeMergeThreshold: 0.40");
            Log("  - MergeToCount: NO (Auto)");
            Log("  - PitchProtection: true");

            var sw = Stopwatch.StartNew();
            var audioMono = await AudioFileLoader.LoadAsFloatAsync(audioPath);
            Log($"Audio loaded: {audioMono.Length / 16000.0f / 60:F2} minutes");

            List<DiarizationSegment> segments;
            using (var sherpa = new SherpaDiarizationService(downloader.SegmentationModelPath, downloader.EmbeddingModelPath))
            {
                sherpa.ClusteringThreshold = 0.85f; // CHANGED
                sherpa.MinDurationOn = 0.15f;
                sherpa.MinDurationOff = 0.10f;
                sherpa.ForcedNumClusters = -1;
                Log($"Initializing Sherpa...");
                sherpa.Initialize(Environment.ProcessorCount);
                
                Log($"Running diarization...");
                segments = await sherpa.DiarizeAsync(audioMono);
                Log($"Sherpa returned {segments.Count} raw segments");
                LogSegmentDistribution(segments, "Raw segments");
            }

            using (var fingerprinter = new SpeakerFingerprintService(downloader.EmbeddingModelPath, Environment.ProcessorCount))
            {
                fingerprinter.EnablePitchProtection = true;
                fingerprinter.SafeMergeThreshold = 0.40f;
                
                // NO MergeToCount - let it be auto
                Log($"\n--- Skipping MergeToCount (Auto mode) ---");
                
                Log($"\n--- CleanupGhostSegments(minDuration=15s) ---");
                var cleaned = fingerprinter.CleanupGhostSegments(segments, 15.0);
                LogSegmentDistribution(cleaned, "After CleanupGhostSegments");
                
                var padded = cleaned.Select(s => s with {
                    Start = s.Start.TotalSeconds > 0.15 ? s.Start - TimeSpan.FromMilliseconds(150) : TimeSpan.Zero,
                    End = s.End + TimeSpan.FromMilliseconds(150)
                }).ToList();

                sw.Stop();
                Log($"\nTotal time: {sw.Elapsed.TotalSeconds:F1}s");
                
                SaveResult("Test4_NormalizedThreshold085Auto", padded);
            }
            
            await File.WriteAllTextAsync(Path.Combine(_outputDir, "Test4_NormalizedThreshold085Auto.log"), _currentLog.ToString());
        }

        static async Task RunTest5_RefinedBalance(string audioPath, ModelDownloader downloader)
        {
            _currentLog.Clear();
            Log("\n###############################################");
            Log("### TEST 5: Refined Balance (0.75 Threshold, 5s Cleanup)");
            Log("###############################################");
            Log($"Audio: {audioPath}");
            Log("Parameters:");
            Log("  - Preprocessing: highpass=80Hz, lowpass=7800Hz, loudnorm");
            Log("  - ClusteringThreshold: 0.75 (REFINED)");
            Log("  - MinDurationOn: 0.10 (SENSITIVE)");
            Log("  - MinDurationOff: 0.30");
            Log("  - ForcedNumClusters: -1 (auto)");
            Log("  - SafeMergeThreshold: 0.40");
            Log("  - MergeToCount: NO (Auto)");
            Log("  - PitchProtection: true");

            var sw = Stopwatch.StartNew();
            var audioMono = await AudioFileLoader.LoadAsFloatAsync(audioPath);
            Log($"Audio loaded: {audioMono.Length / 16000.0f / 60:F2} minutes");

            List<DiarizationSegment> segments;
            using (var sherpa = new SherpaDiarizationService(downloader.SegmentationModelPath, downloader.EmbeddingModelPath))
            {
                sherpa.ClusteringThreshold = 0.75f; // REFINED
                sherpa.MinDurationOn = 0.10f;       // MORE SENSITIVE
                sherpa.MinDurationOff = 0.30f;
                sherpa.ForcedNumClusters = -1;
                Log($"Initializing Sherpa...");
                sherpa.Initialize(Environment.ProcessorCount);
                
                Log($"Running diarization...");
                segments = await sherpa.DiarizeAsync(audioMono);
                Log($"Sherpa returned {segments.Count} raw segments");
                LogSegmentDistribution(segments, "Raw segments");
            }

            using (var fingerprinter = new SpeakerFingerprintService(downloader.EmbeddingModelPath, Environment.ProcessorCount))
            {
                fingerprinter.EnablePitchProtection = true;
                fingerprinter.SafeMergeThreshold = 0.40f;
                
                Log($"\n--- Skipping MergeToCount (Auto mode) ---");
                
                Log($"\n--- CleanupGhostSegments(minDuration=5s) ---");
                var cleaned = fingerprinter.CleanupGhostSegments(segments, 5.0); // 5s instead of 15s
                LogSegmentDistribution(cleaned, "After CleanupGhostSegments");
                
                var padded = cleaned.Select(s => s with {
                    Start = s.Start.TotalSeconds > 0.10 ? s.Start - TimeSpan.FromMilliseconds(100) : TimeSpan.Zero,
                    End = s.End + TimeSpan.FromMilliseconds(100)
                }).ToList();

                sw.Stop();
                Log($"\nTotal time: {sw.Elapsed.TotalSeconds:F1}s");
                
                SaveResult("Test5_RefinedBalance", padded);
            }
            
            await File.WriteAllTextAsync(Path.Combine(_outputDir, "Test5_RefinedBalance.log"), _currentLog.ToString());
        }

        static async Task RunTest6_Sensitive(string audioPath, ModelDownloader downloader)
        {
            _currentLog.Clear();
            Log("\n###############################################");
            Log("### TEST 6: Sensitive Balance (0.70 Threshold, 2s Cleanup)");
            Log("###############################################");
            Log($"Audio: {audioPath}");
            Log("Parameters:");
            Log("  - Preprocessing: highpass=80Hz, lowpass=7800Hz, loudnorm");
            Log("  - ClusteringThreshold: 0.70 (SENSITIVE)");
            Log("  - MinDurationOn: 0.05 (ULTRA SENSITIVE)");
            Log("  - MinDurationOff: 0.60");
            Log("  - ForcedNumClusters: -1 (auto)");
            Log("  - SafeMergeThreshold: 0.40");
            Log("  - MergeToCount: NO (Auto)");
            Log("  - PitchProtection: true");

            var sw = Stopwatch.StartNew();
            var audioMono = await AudioFileLoader.LoadAsFloatAsync(audioPath);
            Log($"Audio loaded: {audioMono.Length / 16000.0f / 60:F2} minutes");

            List<DiarizationSegment> segments;
            using (var sherpa = new SherpaDiarizationService(downloader.SegmentationModelPath, downloader.EmbeddingModelPath))
            {
                sherpa.ClusteringThreshold = 0.70f;
                sherpa.MinDurationOn = 0.05f;
                sherpa.MinDurationOff = 0.60f;
                sherpa.ForcedNumClusters = -1;
                Log($"Initializing Sherpa...");
                sherpa.Initialize(Environment.ProcessorCount);
                
                Log($"Running diarization...");
                segments = await sherpa.DiarizeAsync(audioMono);
                Log($"Sherpa returned {segments.Count} raw segments");
                LogSegmentDistribution(segments, "Raw segments");
            }

            using (var fingerprinter = new SpeakerFingerprintService(downloader.EmbeddingModelPath, Environment.ProcessorCount))
            {
                fingerprinter.EnablePitchProtection = true;
                fingerprinter.SafeMergeThreshold = 0.40f;
                
                Log($"\n--- Skipping MergeToCount (Auto mode) ---");
                
                Log($"\n--- CleanupGhostSegments(minDuration=2s) ---");
                var cleaned = fingerprinter.CleanupGhostSegments(segments, 2.0);
                LogSegmentDistribution(cleaned, "After CleanupGhostSegments");
                
                var padded = cleaned.Select(s => s with {
                    Start = s.Start.TotalSeconds > 0.05 ? s.Start - TimeSpan.FromMilliseconds(50) : TimeSpan.Zero,
                    End = s.End + TimeSpan.FromMilliseconds(50)
                }).ToList();

                sw.Stop();
                Log($"\nTotal time: {sw.Elapsed.TotalSeconds:F1}s");
                
                SaveResult("Test6_Sensitive", padded);
            }
            
            await File.WriteAllTextAsync(Path.Combine(_outputDir, "Test6_Sensitive.log"), _currentLog.ToString());
        }

        static void LogSegmentDistribution(List<DiarizationSegment> segments, string label)
        {
            var bySpeaker = segments.GroupBy(s => s.SpeakerId)
                .Select(g => new {
                    Speaker = g.Key,
                    Count = g.Count(),
                    TotalDuration = g.Sum(s => (s.End - s.Start).TotalSeconds)
                })
                .OrderBy(x => x.Speaker)
                .ToList();

            Log($"{label}:");
            Log($"  Total segments: {segments.Count}");
            Log($"  Unique speakers: {bySpeaker.Count}");
            foreach (var sp in bySpeaker)
            {
                Log($"    Speaker {sp.Speaker}: {sp.Count} segments, {sp.TotalDuration:F1}s total");
            }
        }

        static void SaveResult(string testName, List<DiarizationSegment> segments)
        {
            // Canonicalize IDs to 1, 2, 3...
            var idMap = new Dictionary<int, int>();
            int nextId = 1;
            var final = new List<DiarizationSegment>();
            foreach (var s in segments)
            {
                if (!idMap.ContainsKey(s.SpeakerId)) idMap[s.SpeakerId] = nextId++;
                final.Add(s with { SpeakerId = idMap[s.SpeakerId] });
            }

            string resultFile = Path.Combine(_outputDir, $"{testName}.txt");
            using var writer = new StreamWriter(resultFile);
            writer.WriteLine("Start|End|Speaker");
            foreach (var seg in final)
                writer.WriteLine($"{seg.Start:hh\\:mm\\:ss\\.fff}|{seg.End:hh\\:mm\\:ss\\.fff}|{seg.SpeakerId}");
            
            Log($"Saved result: {resultFile}");
            Log($"Final speaker count: {idMap.Count}");
        }
    }
}
