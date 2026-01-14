using System;
using System.IO;
using System.Threading.Tasks;
using WsprPc.Services;
using WsprPc.Services.Diarization;

namespace FullTranscriptionTest;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("--- Full Transcription End-to-End Test ---");

        string testFile = @"C:\Users\OlleSöderqvist\Downloads\Samtalsinspelning Brandskärbåtar_260109_112119 (2).m4a";
        string whisperModel = @"D:\Appar\wspr-pc\models\kb-whisper-base\ggml-model-q5_0.bin";
        
        if (!File.Exists(testFile))
        {
            Console.WriteLine($"Error: Audio file not found at {testFile}");
            return;
        }

        if (!File.Exists(whisperModel))
        {
            Console.WriteLine($"Error: Whisper model not found at {whisperModel}");
            return;
        }

        // 1. Setup Whisper
        Console.WriteLine("Initializing Whisper...");
        var whisper = new WhisperNetEngine
        {
            ModelPath = whisperModel,
            CpuThreads = Math.Max(1, Environment.ProcessorCount - 2),
            BeamSize = 2,
            Language = "sv"
        };

        // 2. Setup Full Service
        // We use the models already downloaded by the previous test
        // ModelDownloader defaults to BaseDir/third_party/models/sherpa
        // Let's point it to where the other test put them to avoid re-downloading if possible,
        // or just let it handle it.
        var service = new FileTranscriptionService(whisper);

        // 3. Ensure Diarization models
        Console.WriteLine("Checking Diarization models...");
        await service.EnsureModelsAsync(new Progress<(int p, string s)>(val => 
        {
            Console.Write($"\r{val.s} ({val.p}%)   ");
        }));
        Console.WriteLine("\nModels ready.");

        // 4. Run Full Transcription
        Console.WriteLine("Starting Full Transcription (Diarization + Whisper)...");
        Console.WriteLine("This will take a few minutes. Please wait.");
        
        var watch = System.Diagnostics.Stopwatch.StartNew();
        
        var progress = new Progress<(int percent, string status)>(val => 
        {
            Console.WriteLine($"[{val.percent}%] {val.status}");
        });

        try 
        {
            // Forcing 3 speakers as requested/verified
            string result = await service.TranscribeAsync(testFile, expectedSpeakers: 3, progress: progress);
            
            watch.Stop();
            Console.WriteLine($"\n--- Transcription Complete ({watch.Elapsed.TotalMinutes:0.0} min) ---");
            Console.WriteLine(result);
            Console.WriteLine("-----------------------------------------------------");
            
            // Save to file for easy review
            string outputFile = Path.Combine(AppContext.BaseDirectory, "transcription_result.txt");
            await File.WriteAllTextAsync(outputFile, result);
            Console.WriteLine($"Result saved to: {outputFile}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError during transcription: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            service.Dispose();
            whisper.Dispose();
        }
    }
}
