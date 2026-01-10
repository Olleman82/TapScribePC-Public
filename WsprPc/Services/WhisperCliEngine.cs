using System.Diagnostics;
using System.IO;
using NAudio.Wave;

namespace WsprPc.Services;

public sealed class WhisperCliEngine : ITranscriptionEngine
{
    // TODO: Set these paths once you have built whisper.cpp.
    public string? WhisperCliPath { get; set; }
    public string? FallbackCliPath { get; set; }
    public string? ModelPath { get; set; }
    public string? LogDir { get; set; }
    public int BeamSize { get; set; } = 5;
    public int CpuThreads { get; set; } = 4;
    // public string? InitialPrompt { get; set; } // Removed from interface
    private bool _useFallback;

    public bool IsReady =>
        (!string.IsNullOrWhiteSpace(WhisperCliPath) || !string.IsNullOrWhiteSpace(FallbackCliPath)) &&
        !string.IsNullOrWhiteSpace(ModelPath) &&
        (File.Exists(WhisperCliPath ?? string.Empty) || File.Exists(FallbackCliPath ?? string.Empty)) &&
        File.Exists(ModelPath);

    public async Task<string> TranscribeAsync(short[] pcm16, int sampleRate, string? initialPrompt = null)
    {
        if (!IsReady)
            throw new InvalidOperationException("Whisper CLI path or model path is missing.");

        string tempDir = Path.Combine(Path.GetTempPath(), "wsprpc");
        Directory.CreateDirectory(tempDir);

        string baseName = "dictation_" + Guid.NewGuid().ToString("N");
        string wavPath = Path.Combine(tempDir, baseName + ".wav");
        string txtPath = wavPath + ".txt";
        string logDir = string.IsNullOrWhiteSpace(LogDir)
            ? Path.Combine(Environment.CurrentDirectory, "logs")
            : LogDir!;
        Directory.CreateDirectory(logDir);
        string logPath = Path.Combine(logDir, baseName + ".log");

        WriteWav(pcm16, sampleRate, wavPath);

        string? workingDir = Path.GetDirectoryName(WhisperCliPath);
        string? primary = GetUsableCliPath(WhisperCliPath);
        string? fallback = GetUsableCliPath(FallbackCliPath);

        try
        {
            if (_useFallback && fallback != null)
            {
                var fallbackResult = await RunWhisperAsync(fallback, wavPath, txtPath, logPath + ".fallback");
                if (fallbackResult.ExitCode == 0)
                    return ReadOutputOrThrow(txtPath, fallbackResult.LogPath);
            }

            if (primary != null)
            {
                var primaryResult = await RunWhisperAsync(primary, wavPath, txtPath, logPath);
                if (primaryResult.ExitCode == 0)
                    return ReadOutputOrThrow(txtPath, primaryResult.LogPath);

                if (fallback != null && !string.Equals(primary, fallback, StringComparison.OrdinalIgnoreCase))
                {
                    _useFallback = true;
                    var fallbackResult = await RunWhisperAsync(fallback, wavPath, txtPath, logPath + ".fallback");
                    if (fallbackResult.ExitCode == 0)
                        return ReadOutputOrThrow(txtPath, fallbackResult.LogPath);
                }

                throw new InvalidOperationException($"whisper.cpp misslyckades (exit {primaryResult.ExitCode}). Se logg: {primaryResult.LogPath}");
            }

            if (fallback != null)
            {
                var fallbackResult = await RunWhisperAsync(fallback, wavPath, txtPath, logPath + ".fallback");
                if (fallbackResult.ExitCode == 0)
                    return ReadOutputOrThrow(txtPath, fallbackResult.LogPath);
                throw new InvalidOperationException($"whisper.cpp misslyckades (exit {fallbackResult.ExitCode}). Se logg: {fallbackResult.LogPath}");
            }

            throw new InvalidOperationException("Whisper CLI path or model path is missing.");
        }
        finally
        {
            SafeDelete(wavPath);
            SafeDelete(txtPath);
        }
    }

    private static void WriteWav(short[] pcm16, int sampleRate, string path)
    {
        byte[] bytes = new byte[pcm16.Length * 2];
        Buffer.BlockCopy(pcm16, 0, bytes, 0, bytes.Length);

        using var writer = new WaveFileWriter(path, new WaveFormat(sampleRate, 16, 1));
        writer.Write(bytes, 0, bytes.Length);
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Ignore cleanup errors.
        }
    }

    private static string? GetUsableCliPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        return File.Exists(path) ? path : null;
    }

    private static string ReadOutputOrThrow(string txtPath, string logPath)
    {
        if (!File.Exists(txtPath))
            throw new InvalidOperationException($"whisper.cpp did not create a .txt output file. See log: {logPath}");

        string text = File.ReadAllText(txtPath);
        return text.Trim();
    }

    private async Task<WhisperRunResult> RunWhisperAsync(string cliPath, string wavPath, string txtPath, string logPath)
    {
        string? workingDir = Path.GetDirectoryName(cliPath);
        var psi = new ProcessStartInfo
        {
            FileName = cliPath,
            Arguments = $"-m \"{ModelPath}\" -f \"{wavPath}\" -l sv -otxt -t {Math.Max(1, CpuThreads)} -bs {Math.Max(1, BeamSize)}",
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start whisper.cpp CLI.");

        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (!string.IsNullOrWhiteSpace(stdout) || !string.IsNullOrWhiteSpace(stderr))
        {
            try
            {
                await File.WriteAllTextAsync(logPath, stdout + Environment.NewLine + stderr);
            }
            catch
            {
                // Ignore logging errors.
            }
        }

        return new WhisperRunResult(process.ExitCode, logPath);
    }

    private readonly record struct WhisperRunResult(int ExitCode, string LogPath);
}
