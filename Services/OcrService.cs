using PDFtoImage;
using SkiaSharp;
using TesseractOCR;
using TesseractOCR.Enums;
using Hephaistos;
using System.IO;

namespace Hephaistos.Services;

public sealed class OcrService : IDisposable
{
    private readonly Engine _engine;

    public OcrService(string tessDataPath)
    {
        var frenchDataPath =
            Path.Combine(
                tessDataPath,
                "fra.traineddata"
            );

        if (!File.Exists(frenchDataPath))
        {
            throw new FileNotFoundException(
                "Le modèle OCR français fra.traineddata est introuvable.",
                frenchDataPath
            );
        }

        _engine = new Engine(
            tessDataPath,
            Language.French,
            EngineMode.Default
        );
    }

    public string ExtractTextFromPdfPage(
        string pdfPath,
        int pageNumber
    )
    {
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber)
            );
        }

        using var pdfStream =
            File.OpenRead(pdfPath);

        // PDFtoImage utilise des pages indexées à partir de 0.
        using var bitmap =
            Conversion.ToImage(
                pdfStream,
                page: pageNumber - 1,
                options: new RenderOptions(
                    Dpi: HephaistosSettings.OcrDpi
                )
            );

        // On encode l'image en PNG uniquement en mémoire.
        using var imageData =
            bitmap.Encode(
                SKEncodedImageFormat.Png,
                100
            );

        var imageBytes =
            imageData.ToArray();

        using var image =
            TesseractOCR.Pix.Image
                .LoadFromMemory(
                    imageBytes
                );

        using var page =
            _engine.Process(image);

        return page.Text?.Trim() ?? "";
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}