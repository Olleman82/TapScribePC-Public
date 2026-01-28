using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using SherpaOnnx;
using WsprPc.Services.Diarization;

namespace DiarizationDebug
{
    class Program
    {
        static void Main(string[] args)
        {
            // CONFIGURATION
            string sourceFile = @"C:\OBS\2025-12-10_tylenius_intervju.mp3";
            string modelsDir = Path.Combine(AppContext.BaseDirectory, "test_models");
            Directory.CreateDirectory(modelsDir);

            // Time range: 6:00 to 9:30 (360s to 570s)
            double startS = 360; 
            double durationS = 210; 

            Console.WriteLine("=== DIARIZATION STRATEGY TEST ===");
            Console.WriteLine($"Range: {startS}s to {startS+durationS}s");
            Console.WriteLine("Target: Separate Olle (M1), Åsa (F1), and Questioner (M2)");
            
            // 1. Prepare Audio
            Console.WriteLine("Loading and slicing audio...");
            float[] fullAudio = AudioFileLoader.LoadAsFloatAsync(sourceFile).Result;
            
            int startIdx = (int)(startS * 16000);
            int lengthIdx = (int)(durationS * 16000);
            float[] audio = new float[lengthIdx];
            Array.Copy(fullAudio, startIdx, audio, 0, lengthIdx);

            // 2. Define models and thresholds
            var embeddingModel = EnsureModel(modelsDir, "wespeaker", "https://github.com/k2-fsa/sherpa-onnx/releases/download/speaker-recongition-models/wespeaker_en_voxceleb_resnet34.onnx");
            var segmentationModel = EnsureModel(modelsDir, "sherpa-onnx-pyannote-segmentation-3-0", "https://github.com/k2-fsa/sherpa-onnx/releases/download/speaker-segmentation-models/sherpa-onnx-pyannote-segmentation-3-0.tar.bz2");

            float[] thresholds = { 0.35f, 0.40f, 0.45f, 0.50f };

            foreach (var threshold in thresholds)
            {
                RunDiagnosis(audio, segmentationModel, embeddingModel, threshold, startS);
            }
        }

        static void RunDiagnosis(float[] audio, string segModel, string embModel, float threshold, double globalOffset)
        {
            Console.WriteLine($"\n--- Testing Threshold: {threshold} (AUTO speakers) ---");
            
            var config = new OfflineSpeakerDiarizationConfig();
            config.Segmentation.Pyannote.Model = segModel;
            config.Segmentation.NumThreads = 8;
            config.Embedding.Model = embModel;
            config.Embedding.NumThreads = 8;
            config.Clustering.NumClusters = -1; // Auto identify
            config.Clustering.Threshold = threshold;
            config.MinDurationOn = 0.3f;
            config.MinDurationOff = 0.5f;

            try {
                using var diarizer = new OfflineSpeakerDiarization(config);
                var segments = diarizer.Process(audio);

                var speakerStats = new Dictionary<int, double>();
                
                foreach (var seg in segments)
                {
                    double dur = seg.End - seg.Start;
                    if (!speakerStats.ContainsKey(seg.Speaker)) speakerStats[seg.Speaker] = 0;
                    speakerStats[seg.Speaker] += dur;

                    // Only log segments > 1s for visibility, or all if needed
                    if (dur > 0.1)
                        Console.WriteLine($"[{globalOffset + seg.Start:F1}s - {globalOffset + seg.End:F1}s] ID:{seg.Speaker} ({dur:F1}s)");
                }

                Console.WriteLine("\nSpeaker Totals for Threshold " + threshold + ":");
                foreach (var kvp in speakerStats.OrderByDescending(x => x.Value))
                {
                    string label = "Unknown";
                    // Rough heuristics based on facit
                    if (kvp.Value > 80 && kvp.Value < 120) label = "Likely Olle or Speaker-Group";
                    if (kvp.Value > 60 && kvp.Value < 95 && kvp.Key != 0) label = "Likely Åsa";
                    
                    Console.WriteLine($"  ID {kvp.Key}: {kvp.Value:F1}s  ({label})");
                }
            } catch (Exception ex) {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static string EnsureModel(string dir, string name, string url)
        {
            string fileName = Path.GetFileName(new Uri(url).LocalPath);
            string path = Path.Combine(dir, name.Contains("segmentation") ? Path.Combine("sherpa-onnx-pyannote-segmentation-3-0", "model.onnx") : fileName);
            
            if (File.Exists(path)) return path;

            Console.WriteLine($"Downloading {name}...");
            using var client = new System.Net.Http.HttpClient();
            var data = client.GetByteArrayAsync(url).Result;
            
            if (url.EndsWith(".tar.bz2")) {
                string tmp = Path.Combine(dir, "tmp.tar.bz2");
                File.WriteAllBytes(tmp, data);
                Process.Start(new ProcessStartInfo("tar", $"-xjf \"{tmp}\" -C \"{dir}\"") { CreateNoWindow = true }).WaitForExit();
                File.Delete(tmp);
            } else {
                File.WriteAllBytes(path, data);
            }
            return path;
        }
    }
}
