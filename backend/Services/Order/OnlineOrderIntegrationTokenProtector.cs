using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace KasseAPI_Final.Services.Order;

/// <summary>
/// Seals OAuth tokens with the process Data Protection key ring.
/// Purpose <see cref="Purpose"/>. No separate key is introduced.
/// </summary>
public sealed class OnlineOrderIntegrationTokenProtector
{
    public const string Purpose = "Regkasse.OnlineOrderIntegration.OAuthToken.v1";

    private readonly IDataProtector _protector;

    public OnlineOrderIntegrationTokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        var bytes = _protector.Protect(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToBase64String(bytes);
    }

    public string Unprotect(string ciphertext)
    {
        ArgumentException.ThrowIfNullOrEmpty(ciphertext);
        var raw = _protector.Unprotect(Convert.FromBase64String(ciphertext));
        return Encoding.UTF8.GetString(raw);
    }
}
