using System.Net.Http.Json;
using System.Text.Json;
using Hephaistos;
using System.Net.Http;

namespace Hephaistos.Services;

public class EmbeddingService
{
    private readonly HttpClient _http;


    public EmbeddingService(HttpClient http)
    {
        _http = http;
    }

    public async Task<float[]> CreateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default
    )
    {
        var results = await CreateEmbeddingsAsync(
            new[] { text },
            cancellationToken
        );

        return results[0];
    }

    public async Task<List<float[]>> CreateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default
    )
    {
        if (texts.Count == 0)
            return new List<float[]>();

        var request = new
        {
            model = HephaistosSettings.EmbeddingModel,
            input = texts
        };

        var response = await _http.PostAsJsonAsync(
            "http://localhost:11434/api/embed",
            request,
            cancellationToken
        );

        response.EnsureSuccessStatusCode();

        var json =
            await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken
            );

        var embeddings = json
            .GetProperty("embeddings")
            .EnumerateArray()
            .Select(vector =>
                vector
                    .EnumerateArray()
                    .Select(value => value.GetSingle())
                    .ToArray()
            )
            .ToList();

        if (embeddings.Count != texts.Count)
        {
            throw new Exception(
                "Le nombre d'embeddings retourné ne correspond pas au nombre de textes."
            );
        }

        return embeddings;
    }
}
