using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using WsprPc.Services.Ai.Local;

namespace WsprPc.Tests;

public sealed class LocalModelDownloaderTests
{
    [Fact]
    public async Task DownloadAsync_WritesFileAndReportsProgress()
    {
        byte[] payload = "hello-local-model"u8.ToArray();
        using var http = new HttpClient(new StubHandler(payload));
        var downloader = new LocalModelDownloader(http);

        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bin");
        var progressValues = new List<int>();
        try
        {
            await downloader.DownloadAsync("https://example.test/model.gguf", file, new Progress<int>(p => progressValues.Add(p)));

            Assert.True(File.Exists(file));
            Assert.Equal(payload, await File.ReadAllBytesAsync(file));
            Assert.NotEmpty(progressValues);
            Assert.Equal(100, progressValues[^1]);
        }
        finally
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }

    [Fact]
    public void VerifySha256_ReturnsTrueForMatchingHash()
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(file, "abc");
        try
        {
            using var sha = SHA256.Create();
            string hash = Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(file))).ToLowerInvariant();
            Assert.True(LocalModelDownloader.VerifySha256(file, hash));
        }
        finally
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }

    private sealed class StubHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            };
            return Task.FromResult(response);
        }
    }
}
