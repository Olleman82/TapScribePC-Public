using System.IO;
using System.Text.Json;
using WsprPc.Models;

namespace WsprPc.Stores;

public sealed class PromptStore
{
    private readonly string _path;

    public PromptStore(string path)
    {
        _path = path;
    }

    public List<PromptDefinition> Load()
    {
        if (!File.Exists(_path))
            return new List<PromptDefinition>();

        string json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<List<PromptDefinition>>(json) ?? new List<PromptDefinition>();
    }

    public void Save(List<PromptDefinition> prompts)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(prompts, options);
        File.WriteAllText(_path, json);
    }
}
