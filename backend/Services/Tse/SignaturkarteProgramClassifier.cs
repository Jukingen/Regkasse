using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Tse;

/// <summary>Program compliance status for a TSE device (independent of certificate expiry).</summary>
public static class SignaturkarteProgramStatuses
{
    public const string Compliant = "Compliant";
    public const string Open = "Open";
    public const string Excluded = "Excluded";
    public const string Revoked = "Revoked";
}

/// <summary>Classifies devices for the Mai 2027 Signaturkarte program.</summary>
public static class SignaturkarteProgramClassifier
{
    public static bool IsSoftOrDemoOrFake(TseDevice device)
    {
        var provider = TseOptions.NormalizeProviderName(device.Provider);
        if (provider is TseOptions.ProviderFake or TseOptions.ProviderSoft)
            return true;

        var deviceType = device.DeviceType ?? string.Empty;
        if (deviceType.Contains("soft", StringComparison.OrdinalIgnoreCase)
            || deviceType.Contains("demo", StringComparison.OrdinalIgnoreCase)
            || deviceType.Contains("fake", StringComparison.OrdinalIgnoreCase)
            || deviceType.Contains("simulat", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var serial = device.SerialNumber ?? string.Empty;
        return serial.StartsWith("SOFT-", StringComparison.OrdinalIgnoreCase)
            || serial.StartsWith("DEMO-", StringComparison.OrdinalIgnoreCase)
            || serial.StartsWith("FAKE-", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRevoked(TseDevice device) =>
        !device.IsActive
        || string.Equals(device.CertificateStatus, "REVOKED", StringComparison.OrdinalIgnoreCase)
        || string.Equals(device.CertificateStatus, "Revoked", StringComparison.OrdinalIgnoreCase);

    public static string Classify(TseDevice device, bool excludeDemoAndSoft)
    {
        if (IsRevoked(device))
            return SignaturkarteProgramStatuses.Revoked;

        if (excludeDemoAndSoft && IsSoftOrDemoOrFake(device))
            return SignaturkarteProgramStatuses.Excluded;

        if (device.SignaturkarteProgramCompliantAtUtc is not null)
            return SignaturkarteProgramStatuses.Compliant;

        return SignaturkarteProgramStatuses.Open;
    }
}
