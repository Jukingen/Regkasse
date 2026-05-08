using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace KasseAPI_Final.Utils;

/// <summary>
/// Builds a stable machine fingerprint string and SHA-256 hash for license binding.
/// </summary>
public static class MachineFingerprint
{
    /// <summary>
    /// Canonical binding string. Windows with ProductId: <c>ComputerName|Mac|ProductId</c>;
    /// otherwise <c>ComputerName|Mac</c>. Optional motherboard serial appended when requested and available (Windows WMI).
    /// </summary>
    public static string GetCanonicalString(bool includeMotherboardSerial = false)
    {
        var computerName = Environment.MachineName.Trim();
        if (computerName.Length == 0)
            computerName = "UNKNOWN_HOST";

        var mac = GetPreferredMacAddress() ?? "NO_MAC";

        if (OperatingSystem.IsWindows())
            return GetCanonicalStringWindows(computerName, mac, includeMotherboardSerial);

        return string.Join('|', computerName, mac);
    }

    /// <summary>SHA-256 of <see cref="GetCanonicalString"/> as lowercase hex (64 chars).</summary>
    public static string GetSha256Hex(bool includeMotherboardSerial = false)
    {
        var s = GetCanonicalString(includeMotherboardSerial);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Compares <paramref name="expectedHash"/> to the current fingerprint hash (constant-time when lengths match).
    /// </summary>
    public static bool IsMatching(string? expectedHash, bool includeMotherboardSerial = false)
    {
        if (string.IsNullOrWhiteSpace(expectedHash))
            return false;

        var expected = NormalizeHex(expectedHash);
        if (expected.Length != 64)
            return false;

        var actual = GetSha256Hex(includeMotherboardSerial);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(actual));
    }

    private static string NormalizeHex(string hash)
    {
        var sb = new StringBuilder(hash.Length);
        foreach (var c in hash.Trim())
        {
            if (c == ' ' || c == '-')
                continue;
            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    private static string? GetPreferredMacAddress()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()
                     .Where(IsCandidateNic)
                     .OrderBy(n => n.Id, StringComparer.Ordinal))
        {
            var addr = ni.GetPhysicalAddress();
            var b = addr.GetAddressBytes();
            if (b.Length < 6 || b.All(x => x == 0))
                continue;

            return BitConverter.ToString(b).Replace("-", ":", StringComparison.Ordinal);
        }

        return null;
    }

    private static bool IsCandidateNic(NetworkInterface n)
    {
        if (n.OperationalStatus != OperationalStatus.Up)
            return false;
        if (n.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            return false;
        if (IsProbablyVirtual(n))
            return false;
        return true;
    }

    private static bool IsProbablyVirtual(NetworkInterface n)
    {
        var desc = n.Description ?? string.Empty;
        if (desc.Contains("virtual", StringComparison.OrdinalIgnoreCase))
            return true;
        if (desc.Contains("hyper-v", StringComparison.OrdinalIgnoreCase))
            return true;
        if (desc.Contains("vmware", StringComparison.OrdinalIgnoreCase))
            return true;
        if (desc.Contains("virtualbox", StringComparison.OrdinalIgnoreCase))
            return true;
        if (desc.Contains("tap-windows", StringComparison.OrdinalIgnoreCase))
            return true;
        if (desc.Contains("vethernet", StringComparison.OrdinalIgnoreCase))
            return true;
        if (desc.Contains("vpn", StringComparison.OrdinalIgnoreCase) &&
            n.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
            n.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
            return true;

        return n.NetworkInterfaceType is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp;
    }

    [SupportedOSPlatform("windows")]
    private static string GetCanonicalStringWindows(string computerName, string mac, bool includeMotherboardSerial)
    {
        var productId = TryReadWindowsProductId();
        var combined = string.IsNullOrWhiteSpace(productId)
            ? string.Join('|', computerName, mac)
            : string.Join('|', computerName, mac, productId!.Trim());

        if (includeMotherboardSerial)
        {
            var board = TryGetMotherboardSerialWmi();
            if (!string.IsNullOrWhiteSpace(board))
                combined += "|" + board.Trim();
        }

        return combined;
    }

    [SupportedOSPlatform("windows")]
    private static string? TryReadWindowsProductId()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", writable: false);
            return k?.GetValue("ProductId") as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>WMI Win32_BaseBoard.SerialNumber; returns null on failure or placeholder OEM strings.</summary>
    [SupportedOSPlatform("windows")]
    private static string? TryGetMotherboardSerialWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            searcher.Options.Timeout = TimeSpan.FromSeconds(3);

            using var results = searcher.Get();
            foreach (ManagementObject mo in results)
            {
                using (mo)
                {
                    var sn = mo["SerialNumber"]?.ToString();
                    if (string.IsNullOrWhiteSpace(sn))
                        continue;
                    if (IsPlaceholderSerial(sn))
                        continue;
                    return sn.Trim();
                }
            }
        }
        catch
        {
            // WMI unavailable, timeout, or access denied — fingerprint falls back without board serial.
        }

        return null;
    }

    private static bool IsPlaceholderSerial(string sn) =>
        string.Equals(sn, "To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase)
        || string.Equals(sn, "Default string", StringComparison.OrdinalIgnoreCase)
        || string.Equals(sn, "None", StringComparison.OrdinalIgnoreCase)
        || string.Equals(sn, "N/A", StringComparison.OrdinalIgnoreCase);
}
