namespace WsprPc.Models;

public enum AiProvider
{
    Gemini,
    OpenAI
}

public sealed class PromptDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string SystemInstruction { get; set; } = "";
    public string UserInstruction { get; set; } = "";
    public bool UseMemory { get; set; }
    public bool UseClipboard { get; set; }
    public AiProvider Provider { get; set; } = AiProvider.Gemini;
    public string GeminiModel { get; set; } = "models/gemini-flash-latest";
    public bool GeminiUseThinking { get; set; }
    public bool GeminiUseGrounding { get; set; }
    public string OpenAiModel { get; set; } = "gpt-5-mini";
    public string OpenAiReasoningEffort { get; set; } = "minimal";
    
    // Webhook settings
    public bool SendToWebhook { get; set; }
    public string WebhookUrl { get; set; } = "";
    public string WebhookToken { get; set; } = "";
    public bool SendRawText { get; set; }
}
