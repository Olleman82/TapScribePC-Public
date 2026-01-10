namespace WsprPc.Services;

public interface ITranscriptionEngine
{
    bool IsReady { get; }
    Task<string> TranscribeAsync(short[] pcm16, int sampleRate, string? initialPrompt = null);
}
