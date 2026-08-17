namespace Hephaistos;

public static class HephaistosSettings
{
    public const string DisplayVersion =
        "beta 1.2";

    public const int IndexFormatVersion = 1;

    public const string EmbeddingModel =
        "qwen3-embedding:0.6b";
    public const string ChatModel =
        "qwen3:8b";

    public const int ChunkSize = 1500;

    public const int ChunkOverlap = 250;

    public const string OcrLanguage = "fra";

    public const int OcrDpi = 300;

    public const int OcrMinTextCharacters = 80;

    public const string ChunkingAlgorithmVersion =
        "chunker-v2";

    public const string ExtractionAlgorithmVersion =
        "extractor-v1";
}