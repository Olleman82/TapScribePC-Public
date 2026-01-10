using System.Collections.Generic;
using System.IO;

namespace WsprPc.Stores;

public static class EnvLoader
{
    public static Dictionary<string, string> Load(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
            return result;

        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            int idx = line.IndexOf('=');
            if (idx <= 0)
                continue;

            string key = line[..idx].Trim();
            string value = line[(idx + 1)..].Trim();
            if (value.StartsWith('"') && value.EndsWith('"'))
                value = value[1..^1];

            result[key] = value;
        }

        return result;
    }
}
