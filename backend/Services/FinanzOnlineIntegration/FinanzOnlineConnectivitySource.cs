using System.Threading;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// FinanzOnline bağlantı bilgisinin tek kaynağı: şirket ayarları veya yalnızca yapılandırma.
/// </summary>
public sealed class FinanzOnlineConnectivityOptions
{
    public const string SectionName = "FinanzOnline:Connectivity";

    /// <summary>
    /// When true, non-empty ApiUrl / username / password from company_settings override (or fill) config defaults.
    /// </summary>
    public bool UseCompanySettings { get; set; } = false;
}

public sealed class FinanzOnlineConnectivitySnapshot
{
    public string? BaseUrl { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? TelematikId { get; init; }
    public string? HerstellerId { get; init; }
}

public interface IFinanzOnlineConnectivitySource
{
    Task<FinanzOnlineConnectivitySnapshot?> GetCompanySettingsSnapshotAsync(CancellationToken cancellationToken = default);
}

public sealed class DbFinanzOnlineConnectivitySource : IFinanzOnlineConnectivitySource
{
    private readonly AppDbContext _context;
    private readonly IOptionsMonitor<FinanzOnlineConnectivityOptions> _connectivity;
    private readonly ISettingsTenantResolver _settingsTenantResolver;

    public DbFinanzOnlineConnectivitySource(
        AppDbContext context,
        IOptionsMonitor<FinanzOnlineConnectivityOptions> connectivity,
        ISettingsTenantResolver settingsTenantResolver)
    {
        _context = context;
        _connectivity = connectivity;
        _settingsTenantResolver = settingsTenantResolver;
    }

    public async Task<FinanzOnlineConnectivitySnapshot?> GetCompanySettingsSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (!_connectivity.CurrentValue.UseCompanySettings)
            return null;

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var row = await _context.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (row == null)
            return null;

        return new FinanzOnlineConnectivitySnapshot
        {
            BaseUrl = string.IsNullOrWhiteSpace(row.FinanzOnlineApiUrl) ? null : row.FinanzOnlineApiUrl.Trim(),
            Username = string.IsNullOrWhiteSpace(row.FinanzOnlineUsername) ? null : row.FinanzOnlineUsername.Trim(),
            Password = string.IsNullOrWhiteSpace(row.FinanzOnlinePassword) ? null : row.FinanzOnlinePassword,
            TelematikId = string.IsNullOrWhiteSpace(row.FinanzOnlineTelematikId) ? null : row.FinanzOnlineTelematikId.Trim(),
            HerstellerId = string.IsNullOrWhiteSpace(row.FinanzOnlineHerstellerId) ? null : row.FinanzOnlineHerstellerId.Trim()
        };
    }
}
