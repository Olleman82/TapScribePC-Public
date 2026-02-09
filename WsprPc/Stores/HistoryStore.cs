using System.IO;
using System.Text.Json;
using WsprPc.Models;

namespace WsprPc.Stores;

public sealed class HistoryStore
{
    private readonly string _path;
    private List<HistoryItem> _items;

    public HistoryStore(string path)
    {
        _path = path;
        _items = Load();
    }

    public IReadOnlyList<HistoryItem> Items => _items.AsReadOnly();

    private List<HistoryItem> Load()
    {
        if (!File.Exists(_path))
            return new List<HistoryItem>();

        try
        {
            string json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<HistoryItem>>(json) ?? new List<HistoryItem>();
        }
        catch
        {
            return new List<HistoryItem>();
        }
    }

    private void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(_items, options);
        
        string? dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
            
        File.WriteAllText(_path, json);
    }

    public void Add(HistoryItem item)
    {
        _items.Insert(0, item); // Nyast först
        Save();
    }

    public void Delete(IEnumerable<string> ids)
    {
        var idsSet = new HashSet<string>(ids);
        _items.RemoveAll(i => idsSet.Contains(i.Id));
        Save();
    }

    public void Clear()
    {
        _items.Clear();
        Save();
    }

    public void Reload()
    {
        _items = Load();
    }
}
