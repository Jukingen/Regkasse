using System.Security.Cryptography;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Opt-in AES-256-GCM encryption for backup artifact bytes at rest.</summary>
public interface IBackupEncryptionService
{
    bool IsEnabled { get; }

    /// <summary>True when <paramref name="payload"/> starts with the Regkasse backup crypto magic.</summary>
    bool LooksEncrypted(ReadOnlySpan<byte> payload);

    /// <summary>
    /// Encrypts plaintext with a fresh nonce. Wire format:
    /// <c>magic(8) || nonce(12) || tag(16) || ciphertext</c>.
    /// </summary>
    byte[] Encrypt(ReadOnlySpan<byte> plaintext);

    /// <summary>Decrypts a payload produced by <see cref="Encrypt"/>; passthrough when not encrypted.</summary>
    byte[] Decrypt(ReadOnlySpan<byte> payload);

    /// <summary>When enabled, replaces file contents with encrypted bytes. No-op when disabled.</summary>
    Task EncryptFileInPlaceAsync(string absolutePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes decrypted bytes to <paramref name="destinationPath"/>.
    /// If the source is not encrypted, copies as-is.
    /// </summary>
    Task DecryptFileToAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// AES-256-GCM artifact encryption. Key from <see cref="BackupOptions.EncryptionKeyBase64"/> (32 bytes).
/// Disabled by default — enable via <see cref="BackupOptions.EncryptionEnabled"/>.
/// </summary>
public sealed class BackupEncryptionService : IBackupEncryptionService
{
    /// <summary>ASCII <c>RKBAK1\0\0</c> — identifies encrypted backup blobs.</summary>
    public static ReadOnlySpan<byte> Magic => "RKBAK1\0\0"u8;

    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int HeaderSize = 8 + NonceSize + TagSize; // magic + nonce + tag

    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly ILogger<BackupEncryptionService> _logger;

    public BackupEncryptionService(
        IOptionsMonitor<BackupOptions> options,
        ILogger<BackupEncryptionService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsEnabled
    {
        get
        {
            var o = _options.CurrentValue;
            return o.EncryptionEnabled && TryResolveKey(o, out _);
        }
    }

    public bool LooksEncrypted(ReadOnlySpan<byte> payload) =>
        payload.Length >= Magic.Length && payload[..Magic.Length].SequenceEqual(Magic);

    public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
    {
        if (!TryResolveKey(_options.CurrentValue, out var key))
            throw new InvalidOperationException(
                "Backup encryption is not configured (EncryptionEnabled requires a 32-byte EncryptionKeyBase64).");

        Span<byte> nonce = stackalloc byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var cipher = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Encrypt(nonce, plaintext, cipher, tag);
        }

        var output = new byte[HeaderSize + cipher.Length];
        Magic.CopyTo(output.AsSpan(0, Magic.Length));
        nonce.CopyTo(output.AsSpan(Magic.Length, NonceSize));
        tag.CopyTo(output.AsSpan(Magic.Length + NonceSize, TagSize));
        cipher.CopyTo(output.AsSpan(HeaderSize));
        return output;
    }

    public byte[] Decrypt(ReadOnlySpan<byte> payload)
    {
        if (!LooksEncrypted(payload))
            return payload.ToArray();

        if (!TryResolveKey(_options.CurrentValue, out var key))
            throw new CryptographicException(
                "Encrypted backup artifact encountered but EncryptionKeyBase64 is not configured.");

        var nonce = payload.Slice(Magic.Length, NonceSize);
        var tag = payload.Slice(Magic.Length + NonceSize, TagSize);
        var cipher = payload[HeaderSize..];
        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Decrypt(nonce, cipher, tag, plain);
        }

        return plain;
    }

    public async Task EncryptFileInPlaceAsync(string absolutePath, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return;

        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            throw new FileNotFoundException("Backup artifact file not found for encryption.", absolutePath);

        var plain = await File.ReadAllBytesAsync(absolutePath, cancellationToken).ConfigureAwait(false);
        if (LooksEncrypted(plain))
            return;

        var encrypted = Encrypt(plain);
        await File.WriteAllBytesAsync(absolutePath, encrypted, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Backup artifact encrypted at rest: pathLength={PathLength}, plainBytes={PlainBytes}, cipherBytes={CipherBytes}",
            absolutePath.Length,
            plain.Length,
            encrypted.Length);
    }

    public async Task DecryptFileToAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("Backup artifact file not found for decryption.", sourcePath);

        var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var plain = Decrypt(bytes);
        var destDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);
        await File.WriteAllBytesAsync(destinationPath, plain, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns true when Base64 key decodes to exactly 32 bytes.</summary>
    public static bool TryResolveKey(BackupOptions? options, out byte[] key)
    {
        key = Array.Empty<byte>();
        if (options == null || string.IsNullOrWhiteSpace(options.EncryptionKeyBase64))
            return false;

        try
        {
            var decoded = Convert.FromBase64String(options.EncryptionKeyBase64.Trim());
            if (decoded.Length != 32)
                return false;
            key = decoded;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
