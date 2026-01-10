using System.IO;
using System.Text.Json;
using WsprPc.Models;

namespace WsprPc.Stores;

public sealed class MemoryStore
{
    private readonly string _path;

    public MemoryStore(string path)
    {
        _path = path;
    }

    public List<MemoryItem> Load()
    {
        if (!File.Exists(_path))
            return new List<MemoryItem>();

        string json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<List<MemoryItem>>(json) ?? new List<MemoryItem>();
    }

    public void Save(List<MemoryItem> items)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(items, options);
        File.WriteAllText(_path, json);
    }
}
