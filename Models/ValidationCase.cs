namespace Hephaistos.Models;

public class ValidationCase
{
    public string Question { get; set; } = "";

    public string ExpectedDocument { get; set; } = "";

    public int ExpectedPage { get; set; }
}