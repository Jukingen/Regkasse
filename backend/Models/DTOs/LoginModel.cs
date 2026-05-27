using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.DTOs;

/// <summary>POST /api/Auth/login — email or username plus password.</summary>
public class LoginModel
{
    /// <summary>Login with email or username (preferred).</summary>
    [JsonPropertyName("loginIdentifier")]
    public string? LoginIdentifier { get; set; }

    /// <summary>Legacy field; used when <see cref="LoginIdentifier"/> is empty.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Target client application: "pos" or "admin".</summary>
    [JsonPropertyName("clientApp")]
    public string? ClientApp { get; set; }

    /// <summary>Resolved credential: <see cref="LoginIdentifier"/> or legacy <see cref="Email"/>.</summary>
    public string? ResolveLoginIdentifier()
    {
        if (!string.IsNullOrWhiteSpace(LoginIdentifier))
            return LoginIdentifier.Trim();
        if (!string.IsNullOrWhiteSpace(Email))
            return Email.Trim();
        return null;
    }
}
