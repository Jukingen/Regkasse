using KasseAPI_Final.Models;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.DTOs;

/// <summary>Combined POS bootstrap snapshot from <c>GET /api/pos/status/overview</c>.</summary>
public sealed class PosStatusOverviewDto
{
    public DateTime ServerTimeUtc { get; init; }

    /// <summary>Anonymous read model (<c>GET /api/license/status</c> equivalent with mandant overlay).</summary>
    public LicensePublicStatusDto License { get; init; } = new();

    /// <summary>Deployment license health fields (<c>GET /api/health/license</c> subset).</summary>
    public PosStatusLicenseHealthDto HealthLicense { get; init; } = new();

    /// <summary>Read-only cash-register readiness (no auto-open side effects).</summary>
    public PosCashRegisterContextDto CashRegister { get; init; } = new();

    /// <summary>Lightweight user settings revision for client cache invalidation.</summary>
    public PosStatusSettingsSnapshotDto Settings { get; init; } = new();
}

public sealed class PosStatusLicenseHealthDto
{
    public bool IsValid { get; init; }
    public bool IsTrial { get; init; }
    public bool IsExpired { get; init; }
    public int DaysRemaining { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public string MachineHash { get; init; } = string.Empty;
}

public sealed class PosStatusSettingsSnapshotDto
{
    public string? CashRegisterId { get; init; }

    /// <summary>Opaque revision token (<see cref="Models.UserSettings.UpdatedAt"/> UTC ticks).</summary>
    public long SettingsVersion { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}
