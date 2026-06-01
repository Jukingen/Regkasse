using System;
using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.DTOs;

/// <summary>POST /api/Auth/login — username or email plus password.</summary>
public class LoginModel
{
    /// <summary>Login with username or email (preferred).</summary>
    [JsonPropertyName("loginIdentifier")]
    public string? LoginIdentifier { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    /// <summary>Target client application: "pos" or "admin".</summary>
    [JsonPropertyName("clientApp")]
    public string? ClientApp { get; set; }

    /// <summary>Legacy field; used when <see cref="LoginIdentifier"/> is empty.</summary>
    [Obsolete("Use LoginIdentifier instead. Kept for backward compatibility.")]
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>Resolved credential: <see cref="LoginIdentifier"/> or legacy <see cref="Email"/>.</summary>
    public string? ResolveLoginIdentifier()
    {
        if (!string.IsNullOrWhiteSpace(LoginIdentifier))
            return LoginIdentifier.Trim();
#pragma warning disable CS0618 // Legacy email login
        if (!string.IsNullOrWhiteSpace(Email))
            return Email.Trim();
#pragma warning restore CS0618
        return null;
    }
}
