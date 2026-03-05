using System.IO;
using System.Text;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace WsprPc.Services.Ai.Local;

public sealed class LocalQwenClient : IDisposable
{
    private const string BackendRequirementMessage =
        "Den lokala llama-backenden är för gammal för Qwen 3.5. Uppgradera till llama.cpp b7990 eller nyare (rekommenderat b8196/gguf-v0.18.0).";

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private LLamaWeights? _weights;
    private LLamaContext? _context;
    private InteractiveExecutor? _executor;
    private string? _loadedModelPath;
    private string? _runtimeModelPath;

    public async Task<string> GenerateAsync(
        string modelPath,
        string systemInstruction,
        string promptText,
        float temperature,
        int maxTokens,
        int contextSize,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
            throw new FileNotFoundException("Lokal modell hittades inte.", modelPath);

        await EnsureLoadedAsync(modelPath, contextSize, cancellationToken);

        if (_executor == null)
            throw new InvalidOperationException("LLM-session kunde inte initieras.");

        var session = new ChatSession(_executor);

        if (!string.IsNullOrWhiteSpace(systemInstruction))
            session.History.AddMessage(AuthorRole.System, systemInstruction);

        var userMessage = new ChatHistory.Message(AuthorRole.User, promptText);
        var inferenceParams = new InferenceParams
        {
            MaxTokens = Math.Max(1, maxTokens),
            AntiPrompts = ["User:", "### User", "\nUser:"],
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = Math.Clamp(temperature, 0f, 2f)
            }
        };

        var sb = new StringBuilder();
        await foreach (var token in session.ChatAsync(userMessage, inferenceParams, cancellationToken))
        {
            sb.Append(token);
        }

        return sb.ToString().Trim();
    }

    private async Task EnsureLoadedAsync(string modelPath, int contextSize, CancellationToken cancellationToken)
    {
        if (_executor != null && string.Equals(_loadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
            return;

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_executor != null && string.Equals(_loadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
                return;

            DisposeModel();

            _runtimeModelPath = await EnsureAsciiModelPathAsync(modelPath, cancellationToken);
            var modelParams = new ModelParams(_runtimeModelPath)
            {
                ContextSize = (uint)Math.Max(1024, contextSize),
                GpuLayerCount = 0,
                Threads = Math.Max(1, Environment.ProcessorCount / 2)
            };

            try
            {
                _weights = LLamaWeights.LoadFromFile(modelParams);
                _context = _weights.CreateContext(modelParams);
                _executor = new InteractiveExecutor(_context);
                _loadedModelPath = modelPath;
            }
            catch (Exception ex) when (IsBackendIncompatibleForQwen35(modelPath, ex))
            {
                throw new InvalidOperationException(BackendRequirementMessage, ex);
            }
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public void Dispose()
    {
        DisposeModel();
        _loadLock.Dispose();
    }

    private void DisposeModel()
    {
        _executor = null;
        _context?.Dispose();
        _context = null;
        _weights?.Dispose();
        _weights = null;
        _loadedModelPath = null;
        _runtimeModelPath = null;
    }

    private static async Task<string> EnsureAsciiModelPathAsync(string originalPath, CancellationToken cancellationToken)
    {
        bool hasNonAscii = originalPath.Any(c => c > 127);
        if (!hasNonAscii)
            return originalPath;

        string cacheDir = ResolveRuntimeCacheDir();

        string targetPath = Path.Combine(cacheDir, Path.GetFileName(originalPath));
        var sourceInfo = new FileInfo(originalPath);
        var targetInfo = new FileInfo(targetPath);

        if (!targetInfo.Exists || targetInfo.Length != sourceInfo.Length)
        {
            await using var source = File.OpenRead(originalPath);
            await using var target = File.Create(targetPath);
            await source.CopyToAsync(target, cancellationToken);
        }

        return targetPath;
    }

    private static string ResolveRuntimeCacheDir()
    {
        string[] candidates =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Path.GetTempPath()
        ];

        foreach (string root in candidates.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            try
            {
                string candidate = Path.Combine(root, "TapScribe", "local-ai-runtime-cache");
                Directory.CreateDirectory(candidate);
                return candidate;
            }
            catch
            {
                // Try next location.
            }
        }

        throw new InvalidOperationException("Kunde inte skapa cachemapp för lokal AI-modell.");
    }

    private static bool IsBackendIncompatibleForQwen35(string modelPath, Exception ex)
    {
        string fileName = Path.GetFileName(modelPath);
        bool isQwen35 = fileName.Contains("qwen3.5", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("qwen3_5", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("qwen35", StringComparison.OrdinalIgnoreCase);

        if (!isQwen35)
            return false;

        string message = ex.ToString();

        return message.Contains("LLM_ARCH_QWEN35", StringComparison.OrdinalIgnoreCase)
            || message.Contains("qwen35", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unknown architecture", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unknown model architecture", StringComparison.OrdinalIgnoreCase)
            || message.Contains("failed to load model", StringComparison.OrdinalIgnoreCase);
    }
}
