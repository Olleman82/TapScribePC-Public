using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WsprPc.Services.Diarization;

/// <summary>
/// Downloads Sherpa-ONNX diarization models on first use.
/// Models are stored in third_party/models/sherpa/
/// </summary>
public class ModelDownloader
{
    private static readonly HttpClient _http = new();
    
    // Sherpa-ONNX speaker segmentation model
    // URL source: https://github.com/k2-fsa/sherpa-onnx/releases/tag/speaker-segmentation-models
    private const string SegmentationModelUrl = 
        "https://github.com/k2-fsa/sherpa-onnx/releases/download/speaker-segmentation-models/sherpa-onnx-pyannote-segmentation-3-0.tar.bz2";
    
    // Sherpa-ONNX speaker embedding model
    // URL source: https://github.com/k2-fsa/sherpa-onnx/releases/tag/speaker-recongition-models
    private const string EmbeddingModelUrl = 
        "https://github.com/k2-fsa/sherpa-onnx/releases/download/speaker-recongition-models/3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx";
    
    private readonly string _modelsDir;
    
    // The tarball extracts a folder "sherpa-onnx-pyannote-segmentation-3-0", inside is "model.onnx"
    public string SegmentationModelPath => Path.Combine(_modelsDir, "sherpa-onnx-pyannote-segmentation-3-0", "model.onnx");
    public string EmbeddingModelPath => Path.Combine(_modelsDir, "3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx");
    
    public bool ModelsExist => 
        File.Exists(SegmentationModelPath) && 
        File.Exists(EmbeddingModelPath);

    public ModelDownloader(string? basePath = null)
    {
        _modelsDir = basePath ?? Path.Combine(
            AppContext.BaseDirectory, 
            "third_party", 
            "models", 
            "sherpa");
    }

    public async Task<bool> EnsureModelsAsync(
        IProgress<(int percent, string status)>? progress = null,
        CancellationToken ct = default)
    {
        if (ModelsExist)
            return true;

        Directory.CreateDirectory(_modelsDir);

        try
        {
            // 1. Download and Extract Segmentation Model
            if (!File.Exists(SegmentationModelPath))
            {
                string tarPath = Path.Combine(_modelsDir, "segmentation.tar.bz2");
                
                progress?.Report((0, "Laddar ner segmenteringsmodell..."));
                await DownloadFileAsync(
                    SegmentationModelUrl, 
                    tarPath, 
                    percent => progress?.Report((percent / 2, "Laddar ner segmenteringsmodell...")),
                    ct);

                progress?.Report((50, "Packar upp segmenteringsmodell..."));
                await ExtractTarBz2Async(tarPath, _modelsDir);
                
                // Cleanup archive
                try { File.Delete(tarPath); } catch { }
            }

            // 2. Download Embedding Model
            if (!File.Exists(EmbeddingModelPath))
            {
                progress?.Report((60, "Laddar ner talarmodell..."));
                await DownloadFileAsync(
                    EmbeddingModelUrl, 
                    EmbeddingModelPath, 
                    percent => progress?.Report((60 + (int)(percent * 0.4), "Laddar ner talarmodell...")),
                    ct);
            }

            progress?.Report((100, "Klart!"));
            return true;
        }
        catch (Exception)
        {
            // Clean up partial downloads if needed, but be careful not to delete good files
            try 
            {
                if (File.Exists(Path.Combine(_modelsDir, "segmentation.tar.bz2")))
                    File.Delete(Path.Combine(_modelsDir, "segmentation.tar.bz2"));
            } 
            catch { }
            throw;
        }
    }

    private static async Task DownloadFileAsync(
        string url, 
        string destPath,
        Action<int>? progressCallback,
        CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        
        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            totalRead += bytesRead;

            if (totalBytes > 0)
            {
                int percent = (int)(totalRead * 100 / totalBytes);
                progressCallback?.Invoke(percent);
            }
        }
    }

    private async Task ExtractTarBz2Async(string tarPath, string outputDir)
    {
        // Use system tar command (available on Win10+ and checking if it works)
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "tar",
            Arguments = $"-xjf \"{tarPath}\" -C \"{outputDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start tar process.");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            string error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Tar conversion failed: {error}");
        }
    }
}
