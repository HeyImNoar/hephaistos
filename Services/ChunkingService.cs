using Hephaistos.Models;

namespace Hephaistos.Services;

public class ChunkingService
{
    private readonly int _chunkSize;
    private readonly int _overlap;

    public ChunkingService(
        int chunkSize = 1500,
        int overlap = 250
    )
    {
        if (chunkSize <= 0)
            throw new ArgumentException(
                "La taille des chunks doit être supérieure à 0."
            );

        if (overlap < 0 || overlap >= chunkSize)
            throw new ArgumentException(
                "Le chevauchement doit être inférieur à la taille du chunk."
            );

        _chunkSize = chunkSize;
        _overlap = overlap;
    }

    public List<DocumentChunk> CreateChunks(
        IEnumerable<DocumentPage> pages,
        string documentName
    )
    {
        var chunks = new List<DocumentChunk>();

        foreach (var page in pages)
        {
            if (string.IsNullOrWhiteSpace(page.Text))
                continue;

            var pageChunks = SplitText(page.Text);

            for (int i = 0; i < pageChunks.Count; i++)
            {
chunks.Add(new DocumentChunk
{
    DocumentName = documentName,
    PageNumber = page.PageNumber,
    ChunkIndex = i,
    Text = pageChunks[i],
    WasOcr = page.WasOcr
});
            }
        }

        return chunks;
    }

    private List<string> SplitText(string text)
    {
        var chunks = new List<string>();

        text = NormalizeText(text);

        int start = 0;

        while (start < text.Length)
        {
            int proposedEnd = Math.Min(
                start + _chunkSize,
                text.Length
            );

            int end = proposedEnd;

            // Si on n'est pas à la fin du texte,
            // on essaie de couper proprement.
            if (proposedEnd < text.Length)
            {
                end = FindBestBreak(
                    text,
                    start,
                    proposedEnd
                );
            }

            // Sécurité absolue :
            // le chunk doit avancer suffisamment.
            if (end <= start)
            {
                end = proposedEnd;
            }

            var chunk = text[start..end].Trim();

            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }

            if (end >= text.Length)
                break;

            // Si le chunk est trop petit pour permettre
            // un overlap correct, on repart directement à end.
            if (end - start <= _overlap)
            {
                start = end;
            }
            else
            {
                start = end - _overlap;
            }
        }

        return chunks;
    }

    private int FindBestBreak(
        string text,
        int start,
        int proposedEnd
    )
    {
        // On n'accepte une coupure "jolie"
        // que dans les derniers 40 % du chunk.
        int minimumBreak =
            start + (int)(_chunkSize * 0.6);

        minimumBreak = Math.Min(
            minimumBreak,
            proposedEnd
        );

        // 1. Chercher une fin de phrase.
        for (int i = proposedEnd - 1;
             i >= minimumBreak;
             i--)
        {
            if (text[i] == '.' ||
                text[i] == '!' ||
                text[i] == '?' ||
                text[i] == ';')
            {
                return i + 1;
            }
        }

        // 2. Sinon chercher un espace.
        for (int i = proposedEnd - 1;
             i >= minimumBreak;
             i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }

        // 3. Sinon on coupe à la taille prévue.
        return proposedEnd;
    }

    private string NormalizeText(string text)
    {
        return text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Trim();
    }
}