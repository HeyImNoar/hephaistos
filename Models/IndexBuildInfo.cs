namespace Hephaistos.Models;

public class IndexBuildInfo
{
    public int IndexFormatVersion { get; set; }

    public string EmbeddingModel { get; set; } = "";

    public int ChunkSize { get; set; }

    public int ChunkOverlap { get; set; }

    public string OcrLanguage { get; set; } = "";

    public int OcrDpi { get; set; }

    public int OcrMinTextCharacters { get; set; }

    public string ChunkingAlgorithmVersion { get; set; } = "";

    public string ExtractionAlgorithmVersion { get; set; } = "";
}