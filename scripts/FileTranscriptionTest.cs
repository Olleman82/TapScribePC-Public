// Test script for file transcription with diarization
// Usage: dotnet run --project WsprPc/WsprPc.csproj -- --test-file "path/to/audio.m4a"

using System;
using System.IO;
using System.Threading.Tasks;
using WsprPc.Services;
using WsprPc.Services.Diarization;

namespace WsprPc.Tests;

public static class FileTranscriptionTest
{
    public static async Task RunAsync(string audioFilePath)
    {
        Console.WriteLine("=== File Transcription Test ===");
        Console.WriteLine($"Audio file: {audioFilePath}");
        Console.WriteLine();

        if (!File.Exists(audioFilePath))
        {
            Console.WriteLine($"ERROR: File not found: {audioFilePath}");
            return;
        }

        // Check file size
        var fileInfo = new FileInfo(audioFilePath);
        Console.WriteLine($"File size: {fileInfo.Length / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine();

        try
        {
            // Step 1: Load audio
            Console.WriteLine("[1/4] Loading audio file...");
            var audioPcm16 = await AudioFileLoader.LoadAsync(audioFilePath);
            var audioFloat = SherpaDiarizationService.ConvertPcm16ToFloat(audioPcm16);
            double durationSec = audioPcm16.Length / 16000.0;
            Console.WriteLine($"      Duration: {TimeSpan.FromSeconds(durationSec):mm\\:ss}");
            Console.WriteLine($"      Samples: {audioPcm16.Length:N0}");
            Console.WriteLine();

            // Step 2: Initialize Whisper
            Console.WriteLine("[2/4] Initializing Whisper engine...");
            var whisper = new WhisperNetEngine
            {
                Language = "sv",
                CpuThreads = Math.Max(1, Environment.ProcessorCount - 2),
                BeamSize = 2
            };
            
            // Find model
            string modelDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TapScribe", "models");
            
            string[] modelCandidates = {
                Path.Combine(modelDir, "ggml-small.bin"),
                Path.Combine(modelDir, "ggml-base.bin"),
                Path.Combine(modelDir, "ggml-tiny.bin"),
            };
            
            whisper.ModelPath = modelCandidates.FirstOrDefault(File.Exists);
            
            if (string.IsNullOrEmpty(whisper.ModelPath))
            {
                Console.WriteLine("ERROR: No Whisper model found!");
                Console.WriteLine($"Looked in: {modelDir}");
                return;
            }
            Console.WriteLine($"      Model: {Path.GetFileName(whisper.ModelPath)}");
            Console.WriteLine();

            // Step 3: Initialize diarization
            Console.WriteLine("[3/4] Running speaker diarization...");
            string sherpaModelsPath = Path.Combine(
                AppContext.BaseDirectory, "third_party", "models", "sherpa");
            
            var diarizer = new SherpaDiarizationService(
                Path.Combine(sherpaModelsPath, "segmentation-3.0.onnx"),
                Path.Combine(sherpaModelsPath, "3dspeaker_embedding.onnx"));
            
            // Note: We're using placeholder - models may not exist yet
            try
            {
                diarizer.Initialize();
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("      NOTE: Diarization models not downloaded.");
                Console.WriteLine("      Using simplified segmentation (placeholder).");
                // Continue with simplified approach
            }

            var segments = await diarizer.DiarizeAsync(
                audioFloat,
                expectedSpeakers: 2,
                progress: new Progress<int>(p => 
                {
                    if (p % 20 == 0) Console.Write($"\r      Progress: {p}%");
                }));
            
            Console.WriteLine();
            Console.WriteLine($"      Segments found: {segments.Count}");
            Console.WriteLine();

            // Step 4: Transcribe each segment
            Console.WriteLine("[4/4] Transcribing segments...");
            Console.WriteLine();
            
            int segmentNum = 0;
            foreach (var segment in segments)
            {
                segmentNum++;
                Console.WriteLine($"--- Segment {segmentNum} ({segment.Start:mm\\:ss} - {segment.End:mm\\:ss}) ---");
                Console.WriteLine($"[Talare {segment.SpeakerId + 1}]");
                
                var segmentAudio = AudioFileLoader.ExtractSegment(
                    audioPcm16, segment.Start, segment.End);
                
                if (segmentAudio.Length < 1600)
                {
                    Console.WriteLine("(för kort segment, hoppas över)");
                    Console.WriteLine();
                    continue;
                }

                string text = await whisper.TranscribeAsync(
                    segmentAudio, 
                    AudioFileLoader.TargetSampleRate);
                
                Console.WriteLine(text);
                Console.WriteLine();
            }

            Console.WriteLine("=== Test Complete ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
