using System;
using System.IO;

namespace WsprPc.Services;

public sealed class AppLogger
{
    private readonly string _path;
    private readonly object _lock = new();

    public AppLogger(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);

    public void Error(string message, Exception? ex = null)
    {
        var detail = ex == null ? message : $"{message} :: {ex}";
        Write("ERROR", detail);
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        lock (_lock)
        {
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }
}
