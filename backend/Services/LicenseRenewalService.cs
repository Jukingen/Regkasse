using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class LicenseRenewalResult
{
    public bool Success { get; set; }
    public DateTime? NewExpiryDate { get; set; }
    public int DaysAdded { get; set; }
    public int DaysDeducted { get; set; }
    public string Message { get; set; } = string.Empty;
}

public interface ILicenseRenewalService
{
    Task<LicenseRenewalResult> RenewLicenseAsync(
        Guid tenantId,
        int additionalMonths,
        string? actorUserId = null,
        string? actorRole = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Renews mandant <see cref="Models.Tenant.LicenseValidUntilUtc"/> with grace-period day deduction on renewal.
/// </summary>
public sealed class LicenseRenewalService : ILicenseRenewalService
{
    private const int ApproximateDaysPerMonth = 30;

    private readonly AppDbContext _db;
    private readonly ILicenseService _licenseService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<LicenseRenewalService> _logger;

    public LicenseRenewalService(
        AppDbContext db,
        ILicenseService licenseService,
        IAuditLogService auditLogService,
        ILogger<LicenseRenewalService> logger)
    {
        _db = db;
        _licenseService = licenseService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<LicenseRenewalResult> RenewLicenseAsync(
        Guid tenantId,
        int additionalMonths,
        string? actorUserId = null,
        string? actorRole = null,
        CancellationToken cancellationToken = default)
    {
        if (additionalMonths <= 0)
        {
            return new LicenseRenewalResult
            {
                Success = false,
                Message = "additionalMonths must be greater than zero.",
            };
        }

        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (tenant == null)
        {
            return new LicenseRenewalResult { Success = false, Message = "Tenant not found" };
        }

        if (string.Equals(tenant.Status, TenantStatuses.Deleted, StringComparison.OrdinalIgnoreCase))
        {
            return new LicenseRenewalResult { Success = false, Message = "Deleted tenants cannot be renewed." };
        }

        var currentStatus = await _licenseService
            .GetLicenseStatusAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        var totalDaysPurchased = additionalMonths * ApproximateDaysPerMonth;
        var daysDeducted = 0;
        DateTime newExpiryDate;

        if (currentStatus.IsInGracePeriod)
        {
            daysDeducted = currentStatus.DaysOverdue;
            var effectiveDays = Math.Max(0, totalDaysPurchased - daysDeducted);
            newExpiryDate = DateTime.UtcNow.Date.AddDays(effectiveDays);
        }
        else if (currentStatus.DaysRemaining > 0 && currentStatus.ValidUntil.HasValue)
        {
            newExpiryDate = currentStatus.ValidUntil.Value.AddMonths(additionalMonths);
        }
        else if (!currentStatus.ValidUntil.HasValue)
        {
            newExpiryDate = DateTime.UtcNow.Date.AddMonths(additionalMonths);
        }
        else
        {
            newExpiryDate = DateTime.UtcNow.Date.AddMonths(additionalMonths);
        }

        newExpiryDate = DateTime.SpecifyKind(newExpiryDate, DateTimeKind.Utc);
        var previousExpiry = tenant.LicenseValidUntilUtc;
        tenant.LicenseValidUntilUtc = newExpiryDate;
        tenant.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TryAuditRenewalAsync(
            tenantId,
            actorUserId,
            actorRole,
            daysDeducted,
            newExpiryDate,
            previousExpiry,
            additionalMonths,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Tenant {TenantId} license renewed until {Expiry:o}. Months={Months} DaysDeducted={DaysDeducted}",
            tenantId,
            newExpiryDate,
            additionalMonths,
            daysDeducted);

        return new LicenseRenewalResult
        {
            Success = true,
            NewExpiryDate = newExpiryDate,
            DaysAdded = totalDaysPurchased,
            DaysDeducted = daysDeducted,
            Message = $"Lizenz verlängert bis {newExpiryDate:dd.MM.yyyy}",
        };
    }

    private async Task TryAuditRenewalAsync(
        Guid tenantId,
        string? actorUserId,
        string? actorRole,
        int daysDeducted,
        DateTime newExpiryDate,
        DateTime? previousExpiry,
        int additionalMonths,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            return;

        try
        {
            var description = daysDeducted > 0
                ? $"License renewed with {daysDeducted} day(s) deducted from grace period."
                : "License renewed.";

            await _auditLogService.LogSystemOperationAsync(
                AuditLogActions.LICENSE_RENEWED,
                AuditLogEntityTypes.SYSTEM_CONFIG,
                actorUserId,
                actorRole ?? "SuperAdmin",
                description: description,
                requestData: new
                {
                    AdditionalMonths = additionalMonths,
                    UsedGraceDays = daysDeducted,
                    PreviousExpiryUtc = previousExpiry,
                    NewExpiryDate = newExpiryDate,
                },
                actionType: AuditEventType.LicenseRenewed,
                entityId: tenantId,
                tenantId: tenantId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log failed for tenant license renewal TenantId={TenantId}", tenantId);
        }

        _ = cancellationToken;
    }
}
