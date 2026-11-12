namespace KasseAPI_Final.Services.Email;

/// <summary>
/// Log-safe email mask. Keeps the first character and the domain: <c>u***@example.com</c>.
/// </summary>
public static class EmailLogMask
{
    public static string Mask(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "***";

        var trimmed = email.Trim();
        var at = trimmed.IndexOf('@');
        if (at <= 0)
            return "***";

        return trimmed[0] + "***" + trimmed[at..];
    }
}
