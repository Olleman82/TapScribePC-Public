using WsprPc.Services.Ai.Local;

namespace WsprPc.Tests;

public sealed class LocalQwenClientTests
{
    [Fact]
    public async Task GenerateAsync_ThrowsWhenModelMissing()
    {
        using var client = new LocalQwenClient();

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            client.GenerateAsync(
                modelPath: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".gguf"),
                systemInstruction: "Du är hjälpsam.",
                promptText: "Hej",
                temperature: 0.2f,
                maxTokens: 32,
                contextSize: 2048));
    }
}
