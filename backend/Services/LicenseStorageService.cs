using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KasseAPI_Final.Services;

/// <summary>
/// Lisans verisini AES-GCM ile şifrelenmiş bir dosyada saklar; Windows, Linux ve macOS üzerinde aynı kodla çalışır.
/// Registry ve DPAPI bağımlılığı yoktur. Şifreleme anahtarı makineye özgü kimlikten türetilir; dosya başka makineye
/// kopyalansa bile çözümlenemez.
/// </summary>
public interface ILicenseStorageService
{
    /// <summary>Lisans verisini şifreleyip diske yazar; dizin yoksa oluşturur. Atomik replace ile yazılır.</summary>
    void SaveLicenseToFile(LicensePersistedState state);

    /// <summary>Şifreli dosyayı çözüp lisans verisini döner. Dosya yoksa veya çözümlenemiyorsa null döner.</summary>
    LicensePersistedState? LoadLicenseFromFile();

    /// <summary>Lisans dosyasının mutlak yolu (ör. %APPDATA%\Regkasse\license.dat veya ~/.config/Regkasse/license.dat).</summary>
    string LicenseFilePath { get; }

    /// <summary>Makine kimliğinin SHA-256 hash'i (lowercase hex). Lisans bağlama ("machineHash") için kullanılır.</summary>
    string MachineHashHex { get; }

    /// <summary>Makine kimliğinin kanonik metni (yalnızca dahili teşhis için).</summary>
    string MachineFingerprintCanonical { get; }
}

/// <summary>Diskte saklanan lisans durumu (ilk çalışma zamanı, lisans anahtarı, opsiyonel offline JWT).</summary>
public sealed class LicensePersistedState
{
    public DateTime FirstRunUtc { get; set; }

    public string? LicenseKey { get; set; }

    public string? OfflineJwt { get; set; }

    /// <summary>Optional cache of enabled feature ids (JSON array); may be absent on older license files.</summary>
    public string? FeaturesJson { get; set; }
}

/// <summary>
/// Çapraz platform AES-GCM dosya tabanlı lisans deposu.
/// Dosya formatı: <c>[12-byte nonce][16-byte tag][ciphertext]</c>.
/// </summary>
public sealed class LicenseStorageService : ILicenseStorageService
{
    /// <summary>AppData altında kullanılan dizin adı.</summary>
    public const string FolderName = "Regkasse";

    /// <summary>Şifreli lisans dosyasının adı.</summary>
    public const string FileName = "license.dat";

    private const int NonceSize = 12;
    private const int TagSize = 16;

    // Anahtar türetmesinde kullanılan sabit salt; sürüm değişirse yeni bir tane eklenmelidir (geriye dönük uyumsuzluk).
    private static readonly byte[] KeyDerivationSalt = "Regkasse.License.Aes.v1|"u8.ToArray();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly ILogger<LicenseStorageService> _logger;
    private readonly object _ioGate = new();

    private readonly string _filePath;
    private readonly string _machineFingerprint;
    private readonly string _machineHashHex;

    public LicenseStorageService(ILogger<LicenseStorageService> logger)
    {
        _logger = logger;

        // Environment.SpecialFolder.ApplicationData:
        //   Windows -> %APPDATA% (örn: C:\Users\<user>\AppData\Roaming)
        //   Linux   -> $XDG_CONFIG_HOME ya da ~/.config
        //   macOS   -> ~/.config (modern .NET'te) — alternatif olarak ~/Library/Application Support kullanılabilir.
        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _filePath = Path.Combine(appDataRoot, FolderName, FileName);

        (_machineFingerprint, _machineHashHex) = ComputeMachineIdentity();
    }

    public string LicenseFilePath => _filePath;

    public string MachineHashHex => _machineHashHex;

    public string MachineFingerprintCanonical => _machineFingerprint;

    public void SaveLicenseToFile(LicensePersistedState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var plain = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions);
        var encrypted = AesGcmEncrypt(plain);

        lock (_ioGate)
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // Atomik yazım: önce .tmp'ye yaz, sonra hedef dosyayı yer değiştir.
            // Böylece kısmi yazım sırasında mevcut lisans dosyası bozulmaz.
            var tempPath = _filePath + ".tmp";
            File.WriteAllBytes(tempPath, encrypted);
            File.Move(tempPath, _filePath, overwrite: true);
        }

        _logger.LogInformation(
            "License storage: saved encrypted license file at {Path} ({Bytes} bytes).",
            _filePath,
            encrypted.Length);
    }

    public LicensePersistedState? LoadLicenseFromFile()
    {
        byte[] encrypted;
        lock (_ioGate)
        {
            if (!File.Exists(_filePath))
                return null;

            try
            {
                encrypted = File.ReadAllBytes(_filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "License storage: failed to read file at {Path}.", _filePath);
                return null;
            }
        }

        if (encrypted.Length < NonceSize + TagSize + 1)
        {
            _logger.LogWarning(
                "License storage: file at {Path} is too small to contain a valid AES-GCM payload.",
                _filePath);
            return null;
        }

        try
        {
            var plain = AesGcmDecrypt(encrypted);
            return JsonSerializer.Deserialize<LicensePersistedState>(plain, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "License storage: decryption or deserialization failed at {Path}; treating store as missing.",
                _filePath);
            return null;
        }
    }

    private byte[] AesGcmEncrypt(byte[] plain)
    {
        // Her yazımda taze nonce; aynı anahtarla nonce tekrarı AES-GCM güvenliğini bozar.
        Span<byte> nonce = stackalloc byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var tag = new byte[TagSize];
        var cipher = new byte[plain.Length];

        var key = DeriveKey();
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        var output = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(output.AsSpan(0, NonceSize));
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, output, NonceSize + TagSize, cipher.Length);
        return output;
    }

    private byte[] AesGcmDecrypt(byte[] blob)
    {
        var nonce = blob.AsSpan(0, NonceSize);
        var tag = blob.AsSpan(NonceSize, TagSize);
        var cipher = blob.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        var key = DeriveKey();
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return plain;
    }

    private byte[] DeriveKey()
    {
        // Anahtar = SHA-256(salt || makine fingerprint). Makine kimliği değişirse anahtar da değişir,
        // bu da dosyanın başka bir makinede otomatik geçersiz olmasını sağlar.
        var fp = Encoding.UTF8.GetBytes(_machineFingerprint);
        var buf = new byte[KeyDerivationSalt.Length + fp.Length];
        Buffer.BlockCopy(KeyDerivationSalt, 0, buf, 0, KeyDerivationSalt.Length);
        Buffer.BlockCopy(fp, 0, buf, KeyDerivationSalt.Length, fp.Length);
        return SHA256.HashData(buf);
    }

    private static (string Fingerprint, string HashHex) ComputeMachineIdentity()
    {
        var name = string.IsNullOrWhiteSpace(Environment.MachineName)
            ? "UNKNOWN_HOST"
            : Environment.MachineName.Trim();

        var mac = GetFirstActiveNicMac() ?? "NO_MAC";
        var platform = GetPlatformTag();
        var stableId = TryReadCrossPlatformMachineId() ?? "NO_MACHINE_ID";

        var fingerprint = string.Join('|', name, mac, platform, stableId);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint));
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return (fingerprint, hashHex);
    }

    private static string GetPlatformTag()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "WIN";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "LINUX";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "MAC";
        return "OTHER";
    }

    private static string? GetFirstActiveNicMac()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()
                         .Where(n =>
                             n.OperationalStatus == OperationalStatus.Up &&
                             n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                         .OrderBy(n => n.Id, StringComparer.Ordinal))
            {
                var addressBytes = ni.GetPhysicalAddress().GetAddressBytes();
                if (addressBytes.Length < 6) continue;
                if (addressBytes.All(x => x == 0)) continue;
                return BitConverter.ToString(addressBytes).Replace("-", ":", StringComparison.Ordinal);
            }
        }
        catch
        {
            // best-effort: NIC sorgusu başarısız olursa NO_MAC ile devam edilir.
        }

        return null;
    }

    private static string? TryReadCrossPlatformMachineId()
    {
        // Linux/macOS'ta /etc/machine-id veya /var/lib/dbus/machine-id stable bir kimlik sağlar.
        // Windows'ta Registry kullanılmaz; MachineName + MAC + platform tag yeterli kararlılığı sağlar.
        try
        {
            foreach (var path in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" })
            {
                if (File.Exists(path))
                {
                    var content = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(content))
                        return content;
                }
            }
        }
        catch
        {
            // best-effort
        }

        return null;
    }
}
