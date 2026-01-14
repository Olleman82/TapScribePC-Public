using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using WsprPc.Services.Diarization;

namespace DiarizationTest;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("--- Speaker Diarization Test ---");

        string testFile = @"C:\Users\OlleSöderqvist\Downloads\Samtalsinspelning Brandskärbåtar_260109_112119 (2).m4a";
        
        if (!File.Exists(testFile))
        {
            Console.WriteLine($"Error: File not found at {testFile}");
            return;
        }

        Console.WriteLine($"Input file: {testFile}");

        // 1. Setup paths
        string baseDir = AppContext.BaseDirectory;
        // Adjust models path to be relative to where WsprPc usually stores them, or just use local
        // WsprPc usually uses [AppData]/... but ModelDownloader defaults to BaseDirectory/third_party/models/sherpa
        // Let's rely on ModelDownloader's default behavior relative to this executable, 
        // which means we might need to download models again unless we point to the main app's folder.
        // To save time/space, let's point to the main app's third_party folder if possible.
        // But for simplicity/robustness, let's let it download to the test bin folder if it needs to.
        // Or better: Use the one in the main repo structure: D:\Appar\wspr-pc\WsprPc\bin\...\third_party ??
        // Actually, the main app code uses AppContext.BaseDirectory.
        // Let's just let it download to the test output directory. It is 150MB, manageable.
        
        var downloader = new ModelDownloader();
        Console.WriteLine($"Models dir: {downloader.SegmentationModelPath}");

        // 2. Ensure models
        Console.WriteLine("Checking models...");
        await downloader.EnsureModelsAsync(new Progress<(int p, string s)>(val => 
        {
            Console.Write($"\r{val.s} ({val.p}%)   ");
        }));
        Console.WriteLine("\nModels ready.");

        // 3. Initialize Service
        Console.WriteLine("Initializing SherpaDiarizationService...");
        using var service = new SherpaDiarizationService(
            downloader.SegmentationModelPath,
            downloader.EmbeddingModelPath
        );
        service.Initialize();

        // 4. Load Audio
        Console.WriteLine("Loading and converting audio (this may take a moment)...");
        float[] audio;
        try 
        {
            audio = await AudioFileLoader.LoadAsFloatAsync(testFile);
            Console.WriteLine($"Audio loaded. Samples: {audio.Length} ({audio.Length / 16000.0:0.0} seconds)");
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Failed to load audio: {ex.Message}");
            return;
        }

        // 5. Run Diarization
        Console.WriteLine("Running Diarization (forcing 3 speakers)...");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        
        var segments = await service.DiarizeAsync(audio, expectedSpeakers: 3, progress: new Progress<int>(p => 
        {
             // Console.Write($"\rProgress: {p}%");
        }));
        
        watch.Stop();
        Console.WriteLine($"\nDiarization complete in {watch.Elapsed.TotalSeconds:0.00}s");

        // 6. Print Results
        Console.WriteLine("\n--- Segments ---");
        foreach(var seg in segments)
        {
            Console.WriteLine($"[{seg.Start:mm\\:ss\\.f} - {seg.End:mm\\:ss\\.f}] Speaker {seg.SpeakerId}");
        }
        Console.WriteLine("----------------");
        
        // Count speakers
        var speakers = segments.Select(s => s.SpeakerId).Distinct().OrderBy(x => x).ToList();
        Console.WriteLine($"Found {speakers.Count} unique speakers: {string.Join(", ", speakers)}");
    }
}
