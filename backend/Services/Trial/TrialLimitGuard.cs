using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Trial;

public interface ITrialLimitGuard
{
    /// <summary>Throws <see cref="TrialLimitExceededException"/> when an open trial is at register capacity.</summary>
    Task EnsureCanCreateCashRegisterAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Throws <see cref="TrialLimitExceededException"/> when an open trial is at user membership capacity.</summary>
    Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed class TrialLimitGuard : ITrialLimitGuard
{
    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<TrialOptions> _options;

    public TrialLimitGuard(AppDbContext db, IOptionsMonitor<TrialOptions> options)
    {
        _db = db;
        _options = options;
    }

    public async Task EnsureCanCreateCashRegisterAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
            return;

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null || !TrialStatuses.IsOpenTrial(tenant.TrialStatus))
            return;

        var max = Math.Max(1, opts.MaxRegistersInTrial);
        var count = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(r => r.TenantId == tenantId && r.IsActive, cancellationToken)
            .ConfigureAwait(false);

        if (count >= max)
        {
            throw new TrialLimitExceededException(
                "registers",
                max,
                $"Trial limit exceeded: max {max} cash register(s) allowed during trial.");
        }
    }

    public async Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
            return;

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null || !TrialStatuses.IsOpenTrial(tenant.TrialStatus))
            return;

        var max = Math.Max(1, opts.MaxUsersInTrial);
        var count = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(m => m.TenantId == tenantId && m.IsActive, cancellationToken)
            .ConfigureAwait(false);

        if (count >= max)
        {
            throw new TrialLimitExceededException(
                "users",
                max,
                $"Trial limit exceeded: max {max} user(s) allowed during trial.");
        }
    }
}
