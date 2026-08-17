namespace Hephaistos.Models;

public class SourceItem
{
    public string DocumentName { get; set; } = "";

    public string FilePath { get; set; } = "";

    public int PageNumber { get; set; }

    public int ChunkIndex { get; set; }

    public double Score { get; set; }

    // Texte exact du chunk envoyé au LLM
    public string Text { get; set; } = "";

    public string DisplayText =>
    $"{DocumentName} — p. {PageNumber}";
}