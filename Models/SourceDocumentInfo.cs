namespace Hephaistos.Models;

public class SourceDocumentInfo
{
    public string FileName { get; set; } = "";

    public long FileSize { get; set; }

    public DateTime LastWriteTimeUtc { get; set; }
}