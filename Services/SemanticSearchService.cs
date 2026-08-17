using Hephaistos.Models;

namespace Hephaistos.Services;

public class SemanticSearchService
{
    private readonly EmbeddingService _embeddingService;

    public SemanticSearchService(
        EmbeddingService embeddingService
    )
    {
        _embeddingService = embeddingService;
    }

    public async Task IndexChunksAsync(
    List<DocumentChunk> chunks,
    int batchSize = 32,
    IProgress<int>? progress = null,
    CancellationToken cancellationToken = default
)
{
    if (chunks.Count == 0)
    {
        progress?.Report(100);
        return;
    }

    for (int i = 0; i < chunks.Count; i += batchSize)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var batch = chunks
            .Skip(i)
            .Take(batchSize)
            .ToList();

        var texts = batch
            .Select(chunk => chunk.Text)
            .ToArray();

        var embeddings =
            await _embeddingService.CreateEmbeddingsAsync(
                texts,
                cancellationToken
            );

        cancellationToken.ThrowIfCancellationRequested();

        for (int j = 0; j < batch.Count; j++)
        {
            batch[j].Embedding =
                embeddings[j];
        }

        var done =
            Math.Min(
                i + batch.Count,
                chunks.Count
            );

        var percent =
            (int)Math.Round(
                done * 100.0 /
                chunks.Count
            );

        progress?.Report(
            percent
        );
    }
}

    public async Task<List<(DocumentChunk Chunk, double Score)>>
        SearchAsync(
            List<DocumentChunk> chunks,
            string question,
            int topK = 5
        )
    {
        var questionEmbedding =
            await _embeddingService.CreateEmbeddingAsync(
                question
            );

        var results = chunks
            .Where(chunk => chunk.Embedding.Length > 0)
            .Select(chunk =>
            {
                var score =
                    SimilarityService.CosineSimilarity(
                        questionEmbedding,
                        chunk.Embedding
                    );

                return (
                    Chunk: chunk,
                    Score: score
                );
            })
            .OrderByDescending(result => result.Score)
            .Take(topK)
            .ToList();

        return results;
    }
}