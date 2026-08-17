using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hephaistos.Models;
using System.IO;
using System.IO.Compression;

namespace Hephaistos.Services;

public class IndexStorageService
{
    private readonly string _indexDirectory;

    private static readonly byte[] AdditionalEntropy =
        Encoding.UTF8.GetBytes(
            "Hephaistos.Index.v1"
        );

    public IndexStorageService(
        string indexDirectory
    )
    {
        _indexDirectory = indexDirectory;

        Directory.CreateDirectory(
            _indexDirectory
        );
    }

    // ======================================================
    // CHEMIN DU FICHIER D'INDEX
    // ======================================================

    public string GetIndexPath(
        string folderPath
    )
    {
        var fullPath =
            Path.GetFullPath(folderPath)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                );

        var hashBytes =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    fullPath.ToLowerInvariant()
                )
            );

        var hash =
            Convert
                .ToHexString(hashBytes)
                .Substring(0, 16);

        var folderName =
            new DirectoryInfo(
                fullPath
            ).Name;

        return Path.Combine(
            _indexDirectory,
            $"{folderName}-{hash}.index.dpapi"
        );
    }
// ======================================================
// SUPPRESSION D'UN INDEX
// ======================================================

public bool Delete(
    string folderPath
)
{
    var indexPath =
        GetIndexPath(
            folderPath
        );

    if (!File.Exists(indexPath))
    {
        return false;
    }

    File.Delete(
        indexPath
    );

    return true;
}
    // ======================================================
    // SAUVEGARDE
    // ======================================================

    public async Task SaveAsync(
        string folderPath,
        IReadOnlyList<string> pdfPaths,
        List<DocumentChunk> chunks
    )
    {
        var index =
            new DocumentIndex
            {
                FolderName =
                    new DirectoryInfo(
                        folderPath
                    ).Name,

                BuildInfo =
                    CreateCurrentBuildInfo(),

                Documents =
                    CreateDocumentInfos(
                        pdfPaths
                    ),

                Chunks =
                    chunks
            };

        byte[]? compressedBytes = null;

        try
        {
            // Sérialisation JSON directement dans un flux GZip.
            // On évite ainsi de conserver tout le JSON brut
            // dans un byte[] intermédiaire.
            using var compressedStream =
                new MemoryStream();

            await using (
                var gzipStream =
                    new GZipStream(
                        compressedStream,
                        CompressionLevel.Optimal,
                        leaveOpen: true
                    )
            )
            {
                await JsonSerializer.SerializeAsync(
                    gzipStream,
                    index
                );
            }

            compressedBytes =
                compressedStream.ToArray();

            // Chiffrement DPAPI lié à l'utilisateur Windows.
            var encryptedBytes =
                ProtectedData.Protect(
                    compressedBytes,
                    AdditionalEntropy,
                    DataProtectionScope.CurrentUser
                );

            var indexPath =
                GetIndexPath(
                    folderPath
                );

            await File.WriteAllBytesAsync(
                indexPath,
                encryptedBytes
            );
        }
        finally
        {
            if (compressedBytes != null)
            {
                CryptographicOperations.ZeroMemory(
                    compressedBytes
                );
            }
        }
    }

    // ======================================================
    // CHARGEMENT
    // ======================================================

    public async Task<DocumentIndex?> LoadAsync(
        string folderPath
    )
    {
        var indexPath =
            GetIndexPath(
                folderPath
            );

        if (!File.Exists(indexPath))
        {
            return null;
        }

        try
        {
            var encryptedBytes =
                await File.ReadAllBytesAsync(
                    indexPath
                );

            var payloadBytes =
                ProtectedData.Unprotect(
                    encryptedBytes,
                    AdditionalEntropy,
                    DataProtectionScope.CurrentUser
                );

            try
            {
                // Nouveau format :
                // JSON -> GZip -> DPAPI
                if (IsGZipPayload(payloadBytes))
                {
                    using var compressedStream =
                        new MemoryStream(
                            payloadBytes,
                            writable: false
                        );

                    await using var gzipStream =
                        new GZipStream(
                            compressedStream,
                            CompressionMode.Decompress
                        );

                    return await JsonSerializer
                        .DeserializeAsync<DocumentIndex>(
                            gzipStream
                        );
                }

                // Compatibilité avec les anciens index :
                // JSON -> DPAPI
                return JsonSerializer
                    .Deserialize<DocumentIndex>(
                        payloadBytes
                    );
            }
            finally
            {
                CryptographicOperations.ZeroMemory(
                    payloadBytes
                );
            }
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    // ======================================================
    // DÉTECTION DU FORMAT COMPRESSÉ
    // ======================================================

    private static bool IsGZipPayload(
        byte[] bytes
    )
    {
        return
            bytes.Length >= 2 &&
            bytes[0] == 0x1F &&
            bytes[1] == 0x8B;
    }
    // ======================================================
    // COMPATIBILITÉ TECHNIQUE DE L'INDEX
    // ======================================================

    public bool IsBuildCompatible(
        DocumentIndex index
    )
    {
        return IsBuildInfoValid(
            index.BuildInfo
        );
    }

    // ======================================================
    // VALIDATION DE L'INDEX
    // ======================================================

    public bool IsValid(
        string folderPath,
        DocumentIndex index
    )
    {
        // ----------------------------------------------
        // Configuration utilisée pour fabriquer l'index
        // ----------------------------------------------

        if (!IsBuildInfoValid(index.BuildInfo))
        {
            return false;
        }

        // ----------------------------------------------
        // Liste actuelle des PDF
        // ----------------------------------------------

        var pdfPaths =
            Directory
                .GetFiles(
                    folderPath,
                    "*.pdf",
                    SearchOption.TopDirectoryOnly
                )
                .OrderBy(
                    path => path
                )
                .ToList();

        var currentDocuments =
            CreateDocumentInfos(
                pdfPaths
            )
            .OrderBy(
                document => document.FileName
            )
            .ToList();

        var savedDocuments =
            index.Documents
                .OrderBy(
                    document => document.FileName
                )
                .ToList();

        // Un fichier ajouté ou supprimé
        if (
            currentDocuments.Count !=
            savedDocuments.Count
        )
        {
            return false;
        }

        // Un fichier modifié
        for (
            int i = 0;
            i < currentDocuments.Count;
            i++
        )
        {
            var current =
                currentDocuments[i];

            var saved =
                savedDocuments[i];

            if (
                current.FileName != saved.FileName ||
                current.FileSize != saved.FileSize ||
                current.LastWriteTimeUtc != saved.LastWriteTimeUtc
            )
            {
                return false;
            }
        }

        return true;
    }

    // ======================================================
    // CONFIGURATION COURANTE
    // ======================================================

    private static IndexBuildInfo
        CreateCurrentBuildInfo()
    {
        return new IndexBuildInfo
        {
            IndexFormatVersion =
                HephaistosSettings.IndexFormatVersion,

            EmbeddingModel =
                HephaistosSettings.EmbeddingModel,

            ChunkSize =
                HephaistosSettings.ChunkSize,

            ChunkOverlap =
                HephaistosSettings.ChunkOverlap,

            OcrLanguage =
                HephaistosSettings.OcrLanguage,

            OcrDpi =
                HephaistosSettings.OcrDpi,

            OcrMinTextCharacters =
                HephaistosSettings.OcrMinTextCharacters,

            ChunkingAlgorithmVersion =
                HephaistosSettings.ChunkingAlgorithmVersion,

            ExtractionAlgorithmVersion =
                HephaistosSettings.ExtractionAlgorithmVersion
        };
    }

    // ======================================================
    // VALIDATION DE LA CONFIGURATION
    // ======================================================

    private static bool IsBuildInfoValid(
        IndexBuildInfo? saved
    )
    {
        if (saved == null)
        {
            return false;
        }

        return
            saved.IndexFormatVersion ==
                HephaistosSettings.IndexFormatVersion

            && saved.EmbeddingModel ==
                HephaistosSettings.EmbeddingModel

            && saved.ChunkSize ==
                HephaistosSettings.ChunkSize

            && saved.ChunkOverlap ==
                HephaistosSettings.ChunkOverlap

            && saved.OcrLanguage ==
                HephaistosSettings.OcrLanguage

            && saved.OcrDpi ==
                HephaistosSettings.OcrDpi

            && saved.OcrMinTextCharacters ==
                HephaistosSettings.OcrMinTextCharacters

            && saved.ChunkingAlgorithmVersion ==
                HephaistosSettings.ChunkingAlgorithmVersion

            && saved.ExtractionAlgorithmVersion ==
                HephaistosSettings.ExtractionAlgorithmVersion;
    }

    // ======================================================
    // INFORMATIONS SUR LES PDF
    // ======================================================

    private static List<SourceDocumentInfo>
        CreateDocumentInfos(
            IReadOnlyList<string> pdfPaths
        )
    {
        return pdfPaths
            .Select(
                path =>
                {
                    var info =
                        new FileInfo(
                            path
                        );

                    return new SourceDocumentInfo
                    {
                        FileName =
                            info.Name,

                        FileSize =
                            info.Length,

                        LastWriteTimeUtc =
                            info.LastWriteTimeUtc
                    };
                }
            )
            .ToList();
    }
}
