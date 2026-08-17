namespace Hephaistos.Models;

public class ValidationResult
{
    public string Question { get; set; } = "";

    public string ExpectedDocument { get; set; } = "";

    public int ExpectedPage { get; set; }

    public bool Success { get; set; }

    public int? Rank { get; set; }

    public List<string> RetrievedSources { get; set; } = [];
}