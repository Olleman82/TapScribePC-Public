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
    
    // Sherpa-ONNX speaker segmentation model (3Dspeaker)
    private const string SegmentationModelUrl = 
        "https://github.com/k2-fsa/sherpa-onnx/releases/download/speaker-segmentation-models/sherpa-onnx-pyannote-segmentation-3-0.tar.bz2";
    
    // Sherpa-ONNX speaker embedding model
    private const string EmbeddingModelUrl = 
        "https://github.com/k2-fsa/sherpa-onnx/releases/download/speaker-recongition-models/3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx";
    
    private readonly string _modelsDir;
    
    public string SegmentationModelPath => Path.Combine(_modelsDir, "segmentation-3.0.onnx");
    public string EmbeddingModelPath => Path.Combine(_modelsDir, "3dspeaker_embedding.onnx");
    
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
            // Download segmentation model
            progress?.Report((0, "Laddar ner segmenteringsmodell..."));
            await DownloadFileAsync(
                SegmentationModelUrl, 
                SegmentationModelPath, 
                percent => progress?.Report((percent / 2, "Laddar ner segmenteringsmodell...")),
                ct);

            // Download embedding model
            progress?.Report((50, "Laddar ner talarmodell..."));
            await DownloadFileAsync(
                EmbeddingModelUrl, 
                EmbeddingModelPath, 
                percent => progress?.Report((50 + percent / 2, "Laddar ner talarmodell...")),
                ct);

            progress?.Report((100, "Klart!"));
            return true;
        }
        catch (Exception)
        {
            // Clean up partial downloads
            if (File.Exists(SegmentationModelPath))
                File.Delete(SegmentationModelPath);
            if (File.Exists(EmbeddingModelPath))
                File.Delete(EmbeddingModelPath);
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
}
