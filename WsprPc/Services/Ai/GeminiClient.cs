using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace WsprPc.Services.Ai;

public sealed class GeminiClient
{
    private readonly HttpClient _httpClient = new();

    public async Task<string> GenerateAsync(
        string apiKey,
        string model,
        string promptText,
        bool useThinking,
        bool useGrounding,
        CancellationToken cancellationToken = default)
    {
        

        var body = new Dictionary<string, object?>
        {
            ["contents"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["parts"] = new object[]
                    {
                        new Dictionary<string, object?> { ["text"] = promptText }
                    }
                }
            }
        };

        if (useThinking)
        {
            body["generationConfig"] = new Dictionary<string, object?>
            {
                ["thinkingConfig"] = new Dictionary<string, object?>
                {
                    ["thinkingBudget"] = -1
                }
            };
        }
        else
        {
            body["generationConfig"] = new Dictionary<string, object?>
            {
                ["thinkingConfig"] = new Dictionary<string, object?>
                {
                    ["thinkingBudget"] = 0
                }
            };
        }

        if (useGrounding)
        {
            body["tools"] = new object[]
            {
                new Dictionary<string, object?> { ["google_search"] = new Dictionary<string, object?>() }
            };
        }

        string json = JsonSerializer.Serialize(body);
        string content = await SendWithFallbackAsync(apiKey, model, json, cancellationToken);
        return ExtractText(content);
    }

    private async Task<string> SendWithFallbackAsync(
        string apiKey,
        string model,
        string json,
        CancellationToken cancellationToken)
    {
        string modelPath = NormalizeModelPath(model);
        string urlV1Beta = $"https://generativelanguage.googleapis.com/v1beta/{modelPath}:generateContent?key={apiKey}";
        var result = await SendOnceAsync(apiKey, urlV1Beta, json, cancellationToken);
        if (result.success)
            return result.content;

        if (result.statusCode == System.Net.HttpStatusCode.NotFound)
        {
            string urlV1 = $"https://generativelanguage.googleapis.com/v1/{modelPath}:generateContent?key={apiKey}";
            result = await SendOnceAsync(apiKey, urlV1, json, cancellationToken);
            if (result.success)
                return result.content;
        }

        throw new InvalidOperationException(result.content);
    }

    private async Task<(bool success, string content, System.Net.HttpStatusCode statusCode)> SendOnceAsync(
        string apiKey,
        string url,
        string json,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Headers.Add("x-goog-api-client", "tapscribe-pc/0.1");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        string content = await response.Content.ReadAsStringAsync(cancellationToken);
        return (response.IsSuccessStatusCode, content, response.StatusCode);
    }

    private static string NormalizeModelPath(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return "models/gemini-2.5-flash";
        return model.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? model
            : $"models/{model}";
    }

    private static string ExtractText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return string.Empty;

        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content))
            return string.Empty;

        if (!content.TryGetProperty("parts", out var parts))
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text))
                sb.Append(text.GetString());
        }

        return sb.ToString().Trim();
    }
}
