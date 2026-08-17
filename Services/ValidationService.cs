using Hephaistos.Models;
using System.IO;
using System.Text.Json;

namespace Hephaistos.Services;

public class ValidationService
{
    private readonly SemanticSearchService _searchService;

    public ValidationService(
        SemanticSearchService searchService
    )
    {
        _searchService =
            searchService;
    }

    public async Task<List<ValidationCase>>
        LoadValidationCasesAsync(
            string folderPath
        )
    {
        var validationPath =
            Path.Combine(
                folderPath,
                "hephaistos.validation.json"
            );

        if (!File.Exists(validationPath))
        {
            throw new FileNotFoundException(
                "Le fichier hephaistos.validation.json est introuvable.",
                validationPath
            );
        }

        var json =
            await File.ReadAllTextAsync(
                validationPath
            );

        var cases =
            JsonSerializer.Deserialize<List<ValidationCase>>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );

        return cases ?? [];
    }

    public async Task<List<ValidationResult>>
        RunAsync(
            List<DocumentChunk> chunks,
            IReadOnlyList<ValidationCase> validationCases,
            int topK = 5
        )
    {
        var results =
            new List<ValidationResult>();

        // Le benchmark porte uniquement sur les documents
        // explicitement référencés dans hephaistos.validation.json.
        //
        // Ainsi, ajouter dans le même dossier des corpus destinés
        // aux tests de comparaison/contradiction ne modifie pas
        // artificiellement le score de validation de la recherche.
        var validationDocuments =
            validationCases
                .Select(
                    validationCase =>
                        validationCase.ExpectedDocument
                )
                .Where(
                    document =>
                        !string.IsNullOrWhiteSpace(
                            document
                        )
                )
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase
                );

        var validationChunks =
            chunks
                .Where(
                    chunk =>
                        validationDocuments.Contains(
                            chunk.DocumentName
                        )
                )
                .ToList();

        // Sécurité pour d'anciens fichiers de validation
        // ou un éventuel nom de document incorrect.
        if (validationChunks.Count == 0)
        {
            validationChunks =
                chunks;
        }

        foreach (var validationCase in validationCases)
        {
            var searchResults =
                await _searchService.SearchAsync(
                    validationChunks,
                    validationCase.Question,
                    topK
                );

            int? foundRank =
                null;

            for (
                int i = 0;
                i < searchResults.Count;
                i++
            )
            {
                var chunk =
                    searchResults[i].Chunk;

                var sameDocument =
                    string.Equals(
                        chunk.DocumentName,
                        validationCase.ExpectedDocument,
                        StringComparison.OrdinalIgnoreCase
                    );

                var samePage =
                    chunk.PageNumber ==
                    validationCase.ExpectedPage;

                if (
                    sameDocument &&
                    samePage
                )
                {
                    foundRank =
                        i + 1;

                    break;
                }
            }

            results.Add(
                new ValidationResult
                {
                    Question =
                        validationCase.Question,

                    ExpectedDocument =
                        validationCase.ExpectedDocument,

                    ExpectedPage =
                        validationCase.ExpectedPage,

                    Success =
                        foundRank.HasValue,

                    Rank =
                        foundRank,

                    RetrievedSources =
                        searchResults
                            .Select(
                                result =>
                                    $"{result.Chunk.DocumentName}, " +
                                    $"p. {result.Chunk.PageNumber}"
                            )
                            .ToList()
                }
            );
        }

        return results;
    }
}
