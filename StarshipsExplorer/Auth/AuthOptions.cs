namespace StarshipsExplorer.App.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string Username { get; init; } = "admin";
    public string Password { get; init; } = "password";

    public bool IsValid(string? username, string? password) =>
        string.Equals(username, Username, StringComparison.Ordinal) &&
        string.Equals(password, Password, StringComparison.Ordinal);
}

