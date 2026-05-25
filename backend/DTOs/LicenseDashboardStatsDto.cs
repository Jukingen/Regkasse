namespace KasseAPI_Final.DTOs;

/// <summary>Dashboard KPIs for Mandantenlizenz (tenants) and deployment licenses (issued_licenses).</summary>
public sealed class LicenseDashboardStatsDto
{
    /// <summary>Active SaaS tenant licenses (<c>tenants.license_valid_until_utc</c>).</summary>
    public int ActiveTenantLicenses { get; set; }

    public int ExpiringTenantLicenses { get; set; }

    public int ExpiredTenantLicenses { get; set; }

    /// <summary>Active deployment licenses (<c>issued_licenses</c>).</summary>
    public int ActiveDeploymentLicenses { get; set; }

    public int ExpiringDeploymentLicenses { get; set; }

    public int ExpiredDeploymentLicenses { get; set; }

    /// <summary>Distinct active machine fingerprints (<c>activated_licenses</c>).</summary>
    public int ActivatedDevices { get; set; }

    public List<LicenseActivityDto> RecentActivities { get; set; } = [];
}

/// <summary>Recent license lifecycle or activation event for dashboard activity feed.</summary>
public sealed class LicenseActivityDto
{
    public DateTime Timestamp { get; set; }

    /// <summary>Masked REGK display key.</summary>
    public string LicenseKey { get; set; } = string.Empty;

    /// <summary>Short machine fingerprint/hash when available.</summary>
    public string MachineHash { get; set; } = string.Empty;

    /// <summary>E.g. ACTIVATED, GENERATED, REVOKED.</summary>
    public string Action { get; set; } = string.Empty;

    public string UserEmail { get; set; } = string.Empty;
}
