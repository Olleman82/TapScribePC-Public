using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace WsprPc.Services.Ai.Local;

public sealed class LocalLlamaServerClient : IDisposable
{
    private readonly SemaphoreSlim _serverLock = new(1, 1);
    private readonly HttpClient _httpClient = new();
    private Process? _serverProcess;
    private string? _loadedModelPath;
    private int? _loadedContextSize;
    private int? _loadedGpuLayers;
    private string? _baseUrl;

    public async Task<string> GenerateAsync(
        string modelPath,
        string systemInstruction,
        string promptText,
        bool enableThinking,
        float temperature,
        int maxTokens,
        int contextSize,
        int timeoutSeconds,
        int gpuLayers,
        string? configuredServerPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
            throw new FileNotFoundException("Lokal AI-modell hittades inte.", modelPath);

        int effectiveTimeout = Math.Max(10, timeoutSeconds);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(effectiveTimeout));

        await EnsureServerReadyAsync(modelPath, contextSize, gpuLayers, configuredServerPath, timeoutCts.Token);

        if (string.IsNullOrWhiteSpace(_baseUrl))
            throw new InvalidOperationException("llama-server är inte tillgänglig.");

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(systemInstruction))
        {
            messages.Add(new Dictionary<string, object?>
            {
                ["role"] = "system",
                ["content"] = systemInstruction
            });
        }

        messages.Add(new Dictionary<string, object?>
        {
            ["role"] = "user",
            ["content"] = promptText
        });

        var body = new Dictionary<string, object?>
        {
            ["model"] = Path.GetFileName(modelPath),
            ["messages"] = messages,
            ["temperature"] = Math.Clamp(temperature, 0f, 2f),
            ["max_tokens"] = Math.Max(1, maxTokens),
            ["stream"] = false
        };

        if (!enableThinking)
        {
            // Qwen3/Qwen3.5 default is thinking-enabled; explicitly disable for latency-sensitive prompts.
            body["chat_template_kwargs"] = new Dictionary<string, object?> { ["enable_thinking"] = false };
            body["reasoning_budget"] = 0;
            body["reasoning_format"] = "none";
        }

        string json = JsonSerializer.Serialize(body);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
        string content = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"llama-server fel ({(int)response.StatusCode}): {content}");

        return ExtractText(content);
    }

    private async Task EnsureServerReadyAsync(
        string modelPath,
        int contextSize,
        int gpuLayers,
        string? configuredServerPath,
        CancellationToken cancellationToken)
    {
        if (IsServerRunningForModel(modelPath, contextSize, gpuLayers) && await IsHealthyAsync(cancellationToken))
            return;

        await _serverLock.WaitAsync(cancellationToken);
        try
        {
            if (IsServerRunningForModel(modelPath, contextSize, gpuLayers) && await IsHealthyAsync(cancellationToken))
                return;

            StopServerUnsafe();

            int port = FindFreePort();
            int threads = Math.Max(1, Environment.ProcessorCount - 2);
            int ctx = Math.Max(1024, contextSize);
            int preferredGpuLayers = gpuLayers < 0 ? 99 : Math.Max(0, gpuLayers);

            try
            {
                await StartServerAndWaitHealthyAsync(modelPath, configuredServerPath, port, ctx, threads, preferredGpuLayers, cancellationToken);
                return;
            }
            catch when (preferredGpuLayers > 0)
            {
                StopServerUnsafe();
            }

            await StartServerAndWaitHealthyAsync(modelPath, configuredServerPath, port, ctx, threads, 0, cancellationToken);
        }
        finally
        {
            _serverLock.Release();
        }
    }

    private async Task StartServerAndWaitHealthyAsync(
        string modelPath,
        string? configuredServerPath,
        int port,
        int contextSize,
        int threads,
        int gpuLayers,
        CancellationToken cancellationToken)
    {
        string serverExe = ResolveServerExePath(configuredServerPath);
        string arguments = $"-m \"{modelPath}\" --host 127.0.0.1 --port {port} -c {contextSize} -t {threads} -ngl {gpuLayers} --jinja";

        var psi = new ProcessStartInfo
        {
            FileName = serverExe,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(serverExe) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        _serverProcess = Process.Start(psi) ?? throw new InvalidOperationException("Kunde inte starta llama-server.");
        _loadedModelPath = modelPath;
        _loadedContextSize = contextSize;
        _loadedGpuLayers = gpuLayers;
        _baseUrl = $"http://127.0.0.1:{port}";

        await WaitUntilHealthyAsync(cancellationToken);
    }

    private bool IsServerRunningForModel(string modelPath, int contextSize, int gpuLayers)
    {
        int effectiveContext = Math.Max(1024, contextSize);
        int effectiveGpuLayers = gpuLayers < 0 ? 99 : Math.Max(0, gpuLayers);

        return _serverProcess is { HasExited: false }
               && string.Equals(_loadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase)
               && _loadedContextSize == effectiveContext
               && _loadedGpuLayers == effectiveGpuLayers
               && !string.IsNullOrWhiteSpace(_baseUrl);
    }

    private async Task WaitUntilHealthyAsync(CancellationToken cancellationToken)
    {
        for (int i = 0; i < 80; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsHealthyAsync(cancellationToken))
                return;

            if (_serverProcess == null || _serverProcess.HasExited)
                throw new InvalidOperationException("llama-server avslutades under uppstart.");

            await Task.Delay(250, cancellationToken);
        }

        throw new TimeoutException("llama-server blev inte redo i tid.");
    }

    private async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_baseUrl))
            return false;

        try
        {
            using var response = await _httpClient.GetAsync($"{_baseUrl}/v1/models", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveServerExePath(string? configuredServerPath)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(configuredServerPath))
            candidates.Add(configuredServerPath);

        string? envPath = Environment.GetEnvironmentVariable("TAPSCRIBE_LLAMA_SERVER_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
            candidates.Add(envPath);

        candidates.Add(Path.Combine(AppContext.BaseDirectory, "llama-server.exe"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "third_party", "llama-server.exe"));

        string current = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            candidates.Add(Path.Combine(current, "scripts", "experiments", "llama-cpp-b8196", "llama-server.exe"));
            var parent = Directory.GetParent(current);
            if (parent == null)
                break;
            current = parent.FullName;
        }

        string? found = candidates.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(found))
            return found;

        throw new FileNotFoundException(
            "Kunde inte hitta llama-server.exe. Sätt AppConfig.LocalAiServerPath eller miljövariabeln TAPSCRIBE_LLAMA_SERVER_PATH.");
    }

    private static int FindFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string ExtractText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            return string.Empty;

        var first = choices[0];
        if (!first.TryGetProperty("message", out var message))
            return string.Empty;
        if (!message.TryGetProperty("content", out var content))
            return string.Empty;

        return content.GetString()?.Trim() ?? string.Empty;
    }

    private void StopServerUnsafe()
    {
        if (_serverProcess != null)
        {
            try
            {
                if (!_serverProcess.HasExited)
                    _serverProcess.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignore stop failures.
            }
            finally
            {
                _serverProcess.Dispose();
                _serverProcess = null;
            }
        }

        _loadedModelPath = null;
        _loadedContextSize = null;
        _loadedGpuLayers = null;
        _baseUrl = null;
    }

    public void Dispose()
    {
        StopServerUnsafe();
        _serverLock.Dispose();
        _httpClient.Dispose();
    }
}
