namespace Hephaistos.Models;

public class DocumentPage
{
    public int PageNumber { get; set; }

    public string Text { get; set; } = "";

    public bool WasOcr { get; set; }
}