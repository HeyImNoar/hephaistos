namespace Hephaistos.Models;

public class DocumentIndex
{
    public string FolderName { get; set; } = "";

    public IndexBuildInfo BuildInfo { get; set; } = new();

    public List<SourceDocumentInfo> Documents { get; set; } = [];

    public List<DocumentChunk> Chunks { get; set; } = [];
}