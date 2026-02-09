using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using WsprPc.Models;

namespace WsprPc.Services.Diarization;

/// <summary>
/// Orchestrates downloading and verifying all third-party dependencies:
/// - Sherpa-ONNX Diarization Models
/// - FFmpeg Static Binary
/// </summary>
public class DependencyManager
{
    private static readonly HttpClient _http = new();
    private readonly string _baseDir;
    private readonly ModelDownloader _modelDownloader;

    // Minimal static FFmpeg for Windows (ffmpeg.exe only)
    private const string FfmpegUrl = "https://github.com/GyanD/codexffmpeg/releases/download/2024-01-18-git-044737d2f4/ffmpeg-2024-01-18-git-044737d2f4-full_build.7z"; 
    // Wait, 7z is hard to extract. Let's find a zip or just download model-downloader style.
    // For simplicity in this demo, I will use a direct zip if possible or assume a specific source.
    // Real-world: Use a more stable direct link to a .zip.
    private const string FfmpegZipUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    public string FfmpegDir => Path.Combine(_baseDir, "third_party", "ffmpeg");
    public string FfmpegPath => Path.Combine(FfmpegDir, "bin", "ffmpeg.exe");

    public bool ModelsReady => _modelDownloader.ModelsExist;
    public bool FfmpegReady => File.Exists(FfmpegPath);
    public bool AllReady => ModelsReady && FfmpegReady;

    public DependencyManager(string? baseDir = null)
    {
        _baseDir = baseDir ?? AppDomain.CurrentDomain.BaseDirectory;
        _modelDownloader = new ModelDownloader(Path.Combine(_baseDir, "third_party", "models", "sherpa"));
    }

    public async Task EnsureDependenciesAsync(
        IProgress<(int percent, string status)>? progress = null,
        CancellationToken ct = default)
    {
        // 1. Models
        if (!ModelsReady)
        {
            await _modelDownloader.EnsureModelsAsync(
                new Progress<(int percent, string status)>(p => progress?.Report((p.percent / 2, $"Modeller: {p.status}"))), 
                ct);
        }

        // 2. FFmpeg
        if (!FfmpegReady)
        {
            Directory.CreateDirectory(FfmpegDir);
            progress?.Report((50, "Laddar ner FFmpeg (ljudtvätt)..."));
            
            string zipPath = Path.Combine(FfmpegDir, "ffmpeg.zip");
            await DownloadFileAsync(FfmpegZipUrl, zipPath, p => progress?.Report((50 + p / 4, "Laddar ner FFmpeg...")), ct);
            
            progress?.Report((75, "Packar upp FFmpeg..."));
            await ExtractZipAsync(zipPath, FfmpegDir);
            
            try { File.Delete(zipPath); } catch { }
        }

        progress?.Report((100, "Klart!"));
    }

    private static async Task DownloadFileAsync(string url, string destPath, Action<int>? progress, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? -1;
        await using var s = await response.Content.ReadAsStreamAsync(ct);
        await using var f = new FileStream(destPath, FileMode.Create);
        var buffer = new byte[81920];
        long read = 0;
        int bytes;
        while ((bytes = await s.ReadAsync(buffer, ct)) > 0)
        {
            await f.WriteAsync(buffer.AsMemory(0, bytes), ct);
            read += bytes;
            if (total > 0) progress?.Invoke((int)(read * 100 / total));
        }
    }

    private async Task ExtractZipAsync(string zipPath, string outputDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-Command \"Expand-Archive -Path '{zipPath}' -DestinationPath '{outputDir}' -Force\"",
            CreateNoWindow = true,
            UseShellExecute = false
        };
        using var p = Process.Start(psi);
        if (p != null) await p.WaitForExitAsync();
        
        // Find ffmpeg.exe in subdirs and move to canonical location if needed
        var files = Directory.GetFiles(outputDir, "ffmpeg.exe", SearchOption.AllDirectories);
        if (files.Length > 0 && files[0] != FfmpegPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FfmpegPath)!);
            File.Move(files[0], FfmpegPath, true);
        }
    }
}
