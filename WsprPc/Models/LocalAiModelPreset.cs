namespace WsprPc.Models;

public sealed class LocalAiModelPreset
{
    public LocalAiModelPreset(string id, string displayName, string url, string fileName, string subfolder, string note, string? sha256 = null)
    {
        Id = id;
        DisplayName = displayName;
        Url = url;
        FileName = fileName;
        Subfolder = subfolder;
        Note = note;
        Sha256 = sha256;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string Url { get; }
    public string FileName { get; }
    public string Subfolder { get; }
    public string Note { get; }
    public string? Sha256 { get; }
    public bool IsDownloaded { get; set; }
    public string DisplayLabel => DisplayName;
}
