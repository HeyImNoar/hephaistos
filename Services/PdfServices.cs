using Hephaistos.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using System.IO;

namespace Hephaistos.Services;

public class PdfService
{
    private readonly OcrService _ocrService;

    public PdfService(
        OcrService ocrService
    )
    {
        _ocrService = ocrService;
    }

    public List<DocumentPage> ExtractPages(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "Le fichier PDF est introuvable.",
                filePath
            );
        }

        var pages =
            new List<DocumentPage>();

        using var document =
            PdfDocument.Open(filePath);

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var text =
                ContentOrderTextExtractor.GetText(
                    page,
                    true
                );

            var wasOcr = false;

            if (NeedsOcr(text))
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var ocrText =
                        _ocrService.ExtractTextFromPdfPage(
                            filePath,
                            page.Number
                        );

                    cancellationToken.ThrowIfCancellationRequested();

                    if (
                        CountCharacters(ocrText) >
                        CountCharacters(text)
                    )
                    {
                        text = ocrText;
                        wasOcr = true;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // L'échec OCR reste local et n'est pas journalisé.
                }
            }

            pages.Add(
                new DocumentPage
                {
                    PageNumber = page.Number,
                    Text = text,
                    WasOcr = wasOcr
                }
            );
        }

        return pages;
    }

    private static bool NeedsOcr(
        string text
    )
    {
        return
            CountCharacters(text) <
            HephaistosSettings.OcrMinTextCharacters;
    }

    private static int CountCharacters(
        string text
    )
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return text.Count(
            character =>
                !char.IsWhiteSpace(character)
        );
    }
}