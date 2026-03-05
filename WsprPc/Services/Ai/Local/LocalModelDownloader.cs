using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace WsprPc.Services.Ai.Local;

public sealed class LocalModelDownloader
{
    private readonly HttpClient _httpClient;

    public LocalModelDownloader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task DownloadAsync(
        string url,
        string destinationPath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? Environment.CurrentDirectory);

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = File.Create(destinationPath);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;

            if (totalBytes.HasValue && totalBytes.Value > 0)
            {
                int percentage = (int)(totalRead * 100 / totalBytes.Value);
                progress?.Report(Math.Min(100, percentage));
            }
        }

        progress?.Report(100);
    }

    public static bool VerifySha256(string filePath, string expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256))
            return true;

        if (!File.Exists(filePath))
            return false;

        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(stream);
        string actual = Convert.ToHexString(hash).ToLowerInvariant();
        string expected = expectedSha256.Trim().ToLowerInvariant();
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }
}
