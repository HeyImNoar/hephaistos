namespace Hephaistos.Models;

public class ChatMessage
{
    public string Role { get; set; } = "";

    public string Text { get; set; } = "";

    public bool IsUser =>
        string.Equals(
            Role,
            "user",
            StringComparison.OrdinalIgnoreCase
        );

    public string Sender =>
        IsUser
            ? "Vous"
            : "Héphaïstos";
}
