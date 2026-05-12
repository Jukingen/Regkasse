namespace KasseAPI_Final.Configuration;

/// <summary>
/// Optional AES-256 field encryption for voucher plaintext inside offline intent payloads before ASP.NET Data Protection sealing.
/// When unset, only Data Protection applies (legacy behavior).
/// </summary>
public sealed class OfflineVoucherEncryptionOptions
{
    public const string SectionName = "OfflineVoucherEncryption";

    /// <summary>Base64-encoded AES key: 16, 24, or 32 bytes (prefer 32 for AES-256).</summary>
    public string? EncryptionKeyBase64 { get; set; }

    /// <summary>Returns null when disabled, misconfigured, or empty.</summary>
    public static byte[]? TryResolveKeyBytes(OfflineVoucherEncryptionOptions? options)
    {
        if (options == null || string.IsNullOrWhiteSpace(options.EncryptionKeyBase64))
            return null;
        try
        {
            var key = Convert.FromBase64String(options.EncryptionKeyBase64.Trim());
            return key.Length is 16 or 24 or 32 ? key : null;
        }
        catch
        {
            return null;
        }
    }
}
