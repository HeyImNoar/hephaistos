namespace Hephaistos.Models;

public class DocumentChunk
{
    public string DocumentName { get; set; } = "";

    public int PageNumber { get; set; }

    public int ChunkIndex { get; set; }

    public string Text { get; set; } = "";

    public bool WasOcr { get; set; }

    public float[] Embedding { get; set; } = [];
}