using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace KasseAPI_Final.Tse.Fiskaly;

/// <summary>
/// Seals tenant fiskaly API secrets with the process Data Protection key ring.
/// Purpose <see cref="Purpose"/>. Separate from marketplace OAuth tokens.
/// </summary>
public sealed class FiskalyCredentialProtector
{
    public const string Purpose = "Regkasse.Fiskaly.TenantCredential.v1";

    private readonly IDataProtector _protector;

    public FiskalyCredentialProtector(IDataProtectionProvider provider)
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
