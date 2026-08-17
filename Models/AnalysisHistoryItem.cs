namespace Hephaistos.Models;

public sealed class AnalysisHistoryItem
{
    public string Title { get; init; } = "";
    public string Content { get; init; } = "";
    public DateTime CreatedAt { get; init; } = DateTime.Now;

    public string DisplayLabel =>
        $"{CreatedAt:HH:mm:ss} · {Title}";
}
