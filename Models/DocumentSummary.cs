using System.IO;

namespace Hephaistos.Models;

public class DocumentSummary
{
    public string DocumentName { get; set; } = "";

    public string FilePath { get; set; } = "";

    public int IndexedPages { get; set; }

    public int OcrPages { get; set; }

    public int ChunkCount { get; set; }

    public string DetailsText
    {
        get
        {
            var folder =
                string.IsNullOrWhiteSpace(FilePath)
                    ? ""
                    : Path.GetDirectoryName(FilePath) ?? "";

            var details =
                $"{IndexedPages} page(s) — {OcrPages} OCR — {ChunkCount} chunks";

            return
                string.IsNullOrWhiteSpace(folder)
                    ? details
                    : $"{details} — {folder}";
        }
    }

    public string DisplayText =>
        $"{DocumentName} — {DetailsText}";
}
