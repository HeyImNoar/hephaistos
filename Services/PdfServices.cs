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

        LogService.Info(
            $"PDF EXTRACT START | file={Path.GetFileName(filePath)}"
        );

        try
        {
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
                    LogService.Info(
                        $"OCR START | file={Path.GetFileName(filePath)} | page={page.Number} | extractedChars={CountCharacters(text)}"
                    );

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

                            LogService.Info(
                                $"OCR USED | file={Path.GetFileName(filePath)} | page={page.Number} | chars={CountCharacters(ocrText)}"
                            );
                        }
                        else
                        {
                            LogService.Info(
                                $"OCR NOT USED | file={Path.GetFileName(filePath)} | page={page.Number} | ocrChars={CountCharacters(ocrText)}"
                            );
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        LogService.Info(
                            $"OCR CANCELLED | file={Path.GetFileName(filePath)} | page={page.Number}"
                        );

                        throw;
                    }
                    catch (Exception ex)
                    {
                        LogService.Error(
                            $"OCR | file={Path.GetFileName(filePath)} | page={page.Number}",
                            ex
                        );
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

            LogService.Info(
                $"PDF EXTRACT OK | file={Path.GetFileName(filePath)} | pages={pages.Count} | ocrPages={pages.Count(page => page.WasOcr)}"
            );

            return pages;
        }
        catch (OperationCanceledException ex)
        {
            LogService.Error(
                $"PDF EXTRACT CANCELLED | file={Path.GetFileName(filePath)}",
                ex
            );

            throw;
        }
        catch (Exception ex)
        {
            LogService.Error(
                $"PDF EXTRACT | file={Path.GetFileName(filePath)}",
                ex
            );

            throw;
        }
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
