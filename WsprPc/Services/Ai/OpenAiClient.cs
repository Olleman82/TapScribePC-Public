using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace WsprPc.Services.Ai;

public sealed class OpenAiClient
{
    private readonly HttpClient _httpClient = new();

    public async Task<string> GenerateAsync(
        string apiKey,
        string model,
        string systemInstruction,
        string promptText,
        string reasoningEffort,
        CancellationToken cancellationToken = default)
    {
        var input = new List<object>();

        if (!string.IsNullOrWhiteSpace(systemInstruction))
        {
            input.Add(new Dictionary<string, object?>
            {
                ["role"] = "system",
                ["content"] = systemInstruction
            });
        }

        input.Add(new Dictionary<string, object?>
        {
            ["role"] = "user",
            ["content"] = promptText
        });

        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["input"] = input,
            ["reasoning"] = new Dictionary<string, object?>
            {
                ["effort"] = reasoningEffort
            }
        };

        string json = JsonSerializer.Serialize(body);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        string content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(content);

        return ExtractText(content);
    }

    private static string ExtractText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("output_text", out var outputText))
            return outputText.GetString()?.Trim() ?? string.Empty;

        if (!doc.RootElement.TryGetProperty("output", out var output))
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content))
                continue;

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text))
                    sb.Append(text.GetString());
                else if (part.TryGetProperty("output_text", out var outText))
                    sb.Append(outText.GetString());
            }
        }

        return sb.ToString().Trim();
    }
}
