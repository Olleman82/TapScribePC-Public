namespace WsprPc.Models;

public enum HistoryItemType
{
    Transcription,
    AI
}

public sealed class HistoryItem
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Output { get; set; } = string.Empty;
    public HistoryItemType Type { get; set; }

    public HistoryItem() { }

    public HistoryItem(string id, DateTime timestamp, string output, HistoryItemType type)
    {
        Id = id;
        Timestamp = timestamp;
        Output = output;
        Type = type;
    }

    // Convenience properties for UI binding
    public string DateDisplay => Timestamp.ToString("yyyy-MM-dd");
    public string TimeDisplay => Timestamp.ToString("HH:mm:ss");
    public string TypeDisplay => Type == HistoryItemType.Transcription ? "T" : "AI";
}
