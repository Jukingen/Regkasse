using System.Text.Json;
using System.Text.RegularExpressions;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.TenantSettings;

public interface ITenantSettingsService
{
    Task<SettingsChangeResult> RequestSettingsChangeAsync(
        Guid tenantId,
        TenantSettingType settingType,
        JsonElement newValue,
        string reason,
        string requestedBy,
        CancellationToken cancellationToken = default);

    Task<SettingsChangeResult> ApproveSettingsChangeAsync(
        Guid changeId,
        string approvedBy,
        CancellationToken cancellationToken = default);

    Task<SettingsChangeResult> RejectSettingsChangeAsync(
        Guid changeId,
        string approvedBy,
        string reason,
        CancellationToken cancellationToken = default);

    Task<SettingsChangeResult> RevertSettingsChangeAsync(
        Guid changeId,
        string approvedBy,
        string reason,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantSettingsHistoryDto>> GetChangeHistoryAsync(
        Guid tenantId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        TenantSettingStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<CurrentTenantSettingsDto?> GetCurrentSettingsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

public sealed class SettingsChangeResult
{
    public bool Succeeded { get; init; }
    public Guid? ChangeId { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }
    public string? Warning { get; init; }

    public static SettingsChangeResult Success(Guid changeId, string? warning = null) =>
        new()
        {
            Succeeded = true,
            ChangeId = changeId,
            Warning = warning,
        };

    public static SettingsChangeResult Fail(string error, string? errorCode = null) =>
        new()
        {
            Succeeded = false,
            Error = error,
            ErrorCode = errorCode,
        };
}

public static class TenantSettingsErrorCodes
{
    public const string TenantNotFound = "TENANT_NOT_FOUND";
    public const string NoChange = "NO_CHANGE";
    public const string InvalidValue = "INVALID_VALUE";
    public const string PendingExists = "PENDING_EXISTS";
    public const string NotFound = "CHANGE_NOT_FOUND";
    public const string InvalidStatus = "INVALID_STATUS";
    public const string SelfApproval = "SELF_APPROVAL_FORBIDDEN";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string CompanySettingsMissing = "COMPANY_SETTINGS_MISSING";
    public const string CountryLockedFiscal = "COUNTRY_LOCKED_FISCAL";
    public const string CountryNotRksvCompatible = "COUNTRY_NOT_RKSV";
    public const string CurrencyNotRksvCompatible = "CURRENCY_NOT_RKSV";
}

public sealed class TenantSettingsService : ITenantSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>RKSV-compliant currencies for Austrian cash-register deployments.</summary>
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "EUR",
    };

    /// <summary>RKSV-compatible country codes (AT primary; DE for cross-border Super Admin ops).</summary>
    private static readonly HashSet<string> SupportedCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "AT", "DE",
    };

    private static readonly Regex TaxNumberRegex = new(@"^ATU\d{8}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly AppDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly ITenantSettingsNotificationService _notifications;

    public TenantSettingsService(
        AppDbContext db,
        IAuditLogService auditLog,
        ITenantSettingsNotificationService notifications)
    {
        _db = db;
        _auditLog = auditLog;
        _notifications = notifications;
    }

    public async Task<SettingsChangeResult> RequestSettingsChangeAsync(
        Guid tenantId,
        TenantSettingType settingType,
        JsonElement newValue,
        string reason,
        string requestedBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestedBy))
            return SettingsChangeResult.Fail("Requester is required.", "REQUESTER_REQUIRED");

        if (string.IsNullOrWhiteSpace(reason))
            return SettingsChangeResult.Fail("Reason is required.", TenantSettingsErrorCodes.ReasonRequired);

        var tenantExists = await _db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!tenantExists)
            return SettingsChangeResult.Fail("Tenant not found.", TenantSettingsErrorCodes.TenantNotFound);

        var settings = await GetCompanySettingsAsync(tenantId, track: false, cancellationToken).ConfigureAwait(false);
        if (settings is null)
            return SettingsChangeResult.Fail("Company settings not found for tenant.", TenantSettingsErrorCodes.CompanySettingsMissing);

        var settingTypeKey = TenantSettingTypes.ToStorage(settingType);
        var pendingExists = await _db.TenantSettingsHistory.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(
                h => h.TenantId == tenantId
                     && h.SettingType == settingTypeKey
                     && h.Status == TenantSettingStatuses.Pending,
                cancellationToken)
            .ConfigureAwait(false);
        if (pendingExists)
        {
            return SettingsChangeResult.Fail(
                $"A pending {settingTypeKey} change already exists for this tenant.",
                TenantSettingsErrorCodes.PendingExists);
        }

        object currentValue = GetCurrentSettingValue(settings, settingType);
        var (normalizedNew, normalizeError) = NormalizeNewValue(settingType, newValue);
        if (normalizeError is not null)
            return SettingsChangeResult.Fail(normalizeError, TenantSettingsErrorCodes.InvalidValue);

        if (AreEqual(currentValue, normalizedNew!))
            return SettingsChangeResult.Fail("No change detected.", TenantSettingsErrorCodes.NoChange);

        var validation = await ValidateSettingChangeAsync(
                tenantId,
                settingType,
                normalizedNew!,
                cancellationToken)
            .ConfigureAwait(false);
        if (!validation.Succeeded)
            return SettingsChangeResult.Fail(validation.Error!, validation.ErrorCode);

        var now = DateTime.UtcNow;
        var change = new TenantSettingsHistory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SettingType = settingTypeKey,
            OldValue = JsonSerializer.Serialize(currentValue, JsonOptions),
            NewValue = JsonSerializer.Serialize(normalizedNew, JsonOptions),
            Status = TenantSettingStatuses.Pending,
            RequestedBy = requestedBy,
            RequestedAt = now,
            Reason = reason.Trim(),
            CreatedAt = now,
        };

        _db.TenantSettingsHistory.Add(change);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _auditLog.LogSystemOperationAsync(
                AuditLogActions.TENANT_SETTINGS_REQUESTED,
                AuditLogEntityTypes.TENANT_SETTINGS_HISTORY,
                requestedBy,
                RolesOrUnknown(),
                description: $"Requested {settingTypeKey} change",
                notes: reason.Trim(),
                entityId: change.Id,
                tenantId: tenantId,
                oldValues: currentValue,
                newValues: normalizedNew)
            .ConfigureAwait(false);

        await _notifications.NotifySettingsChangeAsync(
                tenantId,
                change.Id,
                ActivityEventType.TenantSettingsChangeRequested,
                settingTypeKey,
                currentValue,
                normalizedNew,
                requestedBy,
                reason.Trim(),
                cancellationToken)
            .ConfigureAwait(false);

        return SettingsChangeResult.Success(change.Id, validation.Warning);
    }

    public async Task<SettingsChangeResult> ApproveSettingsChangeAsync(
        Guid changeId,
        string approvedBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(approvedBy))
            return SettingsChangeResult.Fail("Approver is required.", "APPROVER_REQUIRED");

        var change = await _db.TenantSettingsHistory
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(h => h.Id == changeId, cancellationToken)
            .ConfigureAwait(false);
        if (change is null)
            return SettingsChangeResult.Fail("Change not found.", TenantSettingsErrorCodes.NotFound);

        if (!string.Equals(change.Status, TenantSettingStatuses.Pending, StringComparison.OrdinalIgnoreCase))
        {
            return SettingsChangeResult.Fail(
                $"Change is already {change.Status}.",
                TenantSettingsErrorCodes.InvalidStatus);
        }

        if (string.Equals(change.RequestedBy, approvedBy, StringComparison.OrdinalIgnoreCase))
        {
            return SettingsChangeResult.Fail(
                "Requester cannot approve their own settings change (four-eyes).",
                TenantSettingsErrorCodes.SelfApproval);
        }

        if (!TenantSettingTypes.TryParse(change.SettingType, out var settingType))
            return SettingsChangeResult.Fail("Unknown setting type on change row.", TenantSettingsErrorCodes.InvalidValue);

        var applyResult = await ApplySettingValueAsync(
                change.TenantId,
                settingType,
                change.NewValue,
                cancellationToken)
            .ConfigureAwait(false);
        if (!applyResult.Succeeded)
            return applyResult;

        var now = DateTime.UtcNow;
        change.Status = TenantSettingStatuses.Approved;
        change.ApprovedBy = approvedBy;
        change.ApprovedAt = now;
        change.EffectiveAt = now;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _auditLog.LogSystemOperationAsync(
                AuditLogActions.TENANT_SETTINGS_APPROVED,
                AuditLogEntityTypes.TENANT_SETTINGS_HISTORY,
                approvedBy,
                RolesOrUnknown(),
                description: $"Approved {change.SettingType} change",
                entityId: change.Id,
                tenantId: change.TenantId,
                oldValues: SafeDeserialize(change.OldValue),
                newValues: SafeDeserialize(change.NewValue))
            .ConfigureAwait(false);

        await _notifications.NotifySettingsChangeAsync(
                change.TenantId,
                change.Id,
                ActivityEventType.TenantSettingsChangeApproved,
                change.SettingType,
                SafeDeserialize(change.OldValue),
                SafeDeserialize(change.NewValue),
                approvedBy,
                change.Reason,
                cancellationToken)
            .ConfigureAwait(false);

        return SettingsChangeResult.Success(change.Id);
    }

    public async Task<SettingsChangeResult> RejectSettingsChangeAsync(
        Guid changeId,
        string approvedBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(approvedBy))
            return SettingsChangeResult.Fail("Approver is required.", "APPROVER_REQUIRED");
        if (string.IsNullOrWhiteSpace(reason))
            return SettingsChangeResult.Fail("Rejection reason is required.", TenantSettingsErrorCodes.ReasonRequired);

        var change = await _db.TenantSettingsHistory
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(h => h.Id == changeId, cancellationToken)
            .ConfigureAwait(false);
        if (change is null)
            return SettingsChangeResult.Fail("Change not found.", TenantSettingsErrorCodes.NotFound);

        if (!string.Equals(change.Status, TenantSettingStatuses.Pending, StringComparison.OrdinalIgnoreCase))
        {
            return SettingsChangeResult.Fail(
                $"Change is already {change.Status}.",
                TenantSettingsErrorCodes.InvalidStatus);
        }

        if (string.Equals(change.RequestedBy, approvedBy, StringComparison.OrdinalIgnoreCase))
        {
            return SettingsChangeResult.Fail(
                "Requester cannot reject their own settings change (four-eyes).",
                TenantSettingsErrorCodes.SelfApproval);
        }

        var now = DateTime.UtcNow;
        change.Status = TenantSettingStatuses.Rejected;
        change.ApprovedBy = approvedBy;
        change.ApprovedAt = now;
        change.Notes = reason.Trim();

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _auditLog.LogSystemOperationAsync(
                AuditLogActions.TENANT_SETTINGS_REJECTED,
                AuditLogEntityTypes.TENANT_SETTINGS_HISTORY,
                approvedBy,
                RolesOrUnknown(),
                description: $"Rejected {change.SettingType} change",
                notes: reason.Trim(),
                entityId: change.Id,
                tenantId: change.TenantId)
            .ConfigureAwait(false);

        await _notifications.NotifySettingsChangeAsync(
                change.TenantId,
                change.Id,
                ActivityEventType.TenantSettingsChangeRejected,
                change.SettingType,
                SafeDeserialize(change.OldValue),
                SafeDeserialize(change.NewValue),
                approvedBy,
                reason.Trim(),
                cancellationToken)
            .ConfigureAwait(false);

        return SettingsChangeResult.Success(change.Id);
    }

    public async Task<SettingsChangeResult> RevertSettingsChangeAsync(
        Guid changeId,
        string approvedBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(approvedBy))
            return SettingsChangeResult.Fail("Approver is required.", "APPROVER_REQUIRED");
        if (string.IsNullOrWhiteSpace(reason))
            return SettingsChangeResult.Fail("Revert reason is required.", TenantSettingsErrorCodes.ReasonRequired);

        var change = await _db.TenantSettingsHistory
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(h => h.Id == changeId, cancellationToken)
            .ConfigureAwait(false);
        if (change is null)
            return SettingsChangeResult.Fail("Change not found.", TenantSettingsErrorCodes.NotFound);

        if (!string.Equals(change.Status, TenantSettingStatuses.Approved, StringComparison.OrdinalIgnoreCase))
        {
            return SettingsChangeResult.Fail(
                "Only approved changes can be reverted.",
                TenantSettingsErrorCodes.InvalidStatus);
        }

        if (!TenantSettingTypes.TryParse(change.SettingType, out var settingType))
            return SettingsChangeResult.Fail("Unknown setting type on change row.", TenantSettingsErrorCodes.InvalidValue);

        var applyResult = await ApplySettingValueAsync(
                change.TenantId,
                settingType,
                change.OldValue,
                cancellationToken)
            .ConfigureAwait(false);
        if (!applyResult.Succeeded)
            return applyResult;

        var now = DateTime.UtcNow;
        change.Status = TenantSettingStatuses.Reverted;
        change.ApprovedBy = approvedBy;
        change.ApprovedAt = now;
        change.EffectiveAt = now;
        change.Notes = reason.Trim();

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _auditLog.LogSystemOperationAsync(
                AuditLogActions.TENANT_SETTINGS_REVERTED,
                AuditLogEntityTypes.TENANT_SETTINGS_HISTORY,
                approvedBy,
                RolesOrUnknown(),
                description: $"Reverted {change.SettingType} change",
                notes: reason.Trim(),
                entityId: change.Id,
                tenantId: change.TenantId,
                oldValues: SafeDeserialize(change.NewValue),
                newValues: SafeDeserialize(change.OldValue))
            .ConfigureAwait(false);

        await _notifications.NotifySettingsChangeAsync(
                change.TenantId,
                change.Id,
                ActivityEventType.TenantSettingsChangeReverted,
                change.SettingType,
                SafeDeserialize(change.NewValue),
                SafeDeserialize(change.OldValue),
                approvedBy,
                reason.Trim(),
                cancellationToken)
            .ConfigureAwait(false);

        return SettingsChangeResult.Success(change.Id);
    }

    public async Task<IReadOnlyList<TenantSettingsHistoryDto>> GetChangeHistoryAsync(
        Guid tenantId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        TenantSettingStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.TenantSettingsHistory.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(h => h.TenantId == tenantId);

        if (fromDate.HasValue)
            query = query.Where(h => h.RequestedAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(h => h.RequestedAt <= toDate.Value);
        if (status.HasValue)
        {
            var statusKey = TenantSettingStatuses.ToStorage(status.Value);
            query = query.Where(h => h.Status == statusKey);
        }

        var rows = await query
            .OrderByDescending(h => h.RequestedAt)
            .Take(500)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(TenantSettingsHistoryMapping.ToDto).ToList();
    }

    public async Task<CurrentTenantSettingsDto?> GetCurrentSettingsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetCompanySettingsAsync(tenantId, track: false, cancellationToken).ConfigureAwait(false);
        if (settings is null)
            return null;

        return new CurrentTenantSettingsDto
        {
            TenantId = tenantId,
            Currency = settings.Currency,
            Country = settings.Country,
            TimeZone = settings.TimeZone,
            FiscalSettings = MapFiscal(settings),
            HasFiscalData = await HasSignedFiscalPaymentsAsync(tenantId, cancellationToken).ConfigureAwait(false),
            HasInvoices = await HasInvoicesAsync(tenantId, cancellationToken).ConfigureAwait(false),
        };
    }

    private async Task<SettingsChangeResult> ApplySettingValueAsync(
        Guid tenantId,
        TenantSettingType settingType,
        string? jsonValue,
        CancellationToken cancellationToken)
    {
        var settings = await GetCompanySettingsAsync(tenantId, track: true, cancellationToken).ConfigureAwait(false);
        if (settings is null)
            return SettingsChangeResult.Fail("Company settings not found for tenant.", TenantSettingsErrorCodes.CompanySettingsMissing);

        switch (settingType)
        {
            case TenantSettingType.Currency:
            {
                var currency = DeserializeScalarString(jsonValue);
                if (string.IsNullOrWhiteSpace(currency))
                    return SettingsChangeResult.Fail("Currency value is missing.", TenantSettingsErrorCodes.InvalidValue);
                settings.Currency = currency.Trim().ToUpperInvariant();
                await SyncLocalizationCurrencyAsync(tenantId, settings.Currency, cancellationToken).ConfigureAwait(false);
                break;
            }
            case TenantSettingType.Country:
            {
                var country = DeserializeScalarString(jsonValue);
                if (string.IsNullOrWhiteSpace(country))
                    return SettingsChangeResult.Fail("Country value is missing.", TenantSettingsErrorCodes.InvalidValue);
                settings.Country = country.Trim().ToUpperInvariant();
                break;
            }
            case TenantSettingType.Timezone:
            {
                var tz = DeserializeScalarString(jsonValue);
                if (string.IsNullOrWhiteSpace(tz))
                    return SettingsChangeResult.Fail("Timezone value is missing.", TenantSettingsErrorCodes.InvalidValue);
                settings.TimeZone = tz.Trim();
                await SyncLocalizationTimeZoneAsync(tenantId, settings.TimeZone, cancellationToken).ConfigureAwait(false);
                break;
            }
            case TenantSettingType.FiscalSettings:
            {
                var fiscal = JsonSerializer.Deserialize<FiscalSettingsValueDto>(jsonValue ?? "{}", JsonOptions);
                if (fiscal is null)
                    return SettingsChangeResult.Fail("Fiscal settings value is missing.", TenantSettingsErrorCodes.InvalidValue);
                ApplyFiscal(settings, fiscal);
                break;
            }
            default:
                return SettingsChangeResult.Fail("Unknown setting type.", TenantSettingsErrorCodes.InvalidValue);
        }

        settings.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return SettingsChangeResult.Success(Guid.Empty);
    }

    private async Task SyncLocalizationCurrencyAsync(Guid tenantId, string currency, CancellationToken ct)
    {
        var loc = await _db.LocalizationSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId, ct)
            .ConfigureAwait(false);
        if (loc is null)
            return;
        loc.DefaultCurrency = currency;
        loc.UpdatedAt = DateTime.UtcNow;
    }

    private async Task SyncLocalizationTimeZoneAsync(Guid tenantId, string timeZone, CancellationToken ct)
    {
        var loc = await _db.LocalizationSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId, ct)
            .ConfigureAwait(false);
        if (loc is null)
            return;
        loc.DefaultTimeZone = timeZone;
        loc.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<CompanySettings?> GetCompanySettingsAsync(Guid tenantId, bool track, CancellationToken ct)
    {
        var query = track
            ? _db.CompanySettings.IgnoreQueryFilters()
            : _db.CompanySettings.AsNoTracking().IgnoreQueryFilters();
        return await query.FirstOrDefaultAsync(c => c.TenantId == tenantId, ct).ConfigureAwait(false);
    }

    private async Task<SettingsChangeResult> ValidateSettingChangeAsync(
        Guid tenantId,
        TenantSettingType settingType,
        object newValue,
        CancellationToken cancellationToken = default)
    {
        switch (settingType)
        {
            case TenantSettingType.Currency:
                return await ValidateCurrencyChangeAsync(tenantId, (string)newValue, cancellationToken)
                    .ConfigureAwait(false);
            case TenantSettingType.Country:
                return await ValidateCountryChangeAsync(tenantId, (string)newValue, cancellationToken)
                    .ConfigureAwait(false);
            case TenantSettingType.Timezone:
                return ValidateTimezoneChange((string)newValue);
            case TenantSettingType.FiscalSettings:
                return await ValidateFiscalChangeAsync(tenantId, (FiscalSettingsValueDto)newValue, cancellationToken)
                    .ConfigureAwait(false);
            default:
                return SettingsChangeResult.Fail("Unknown setting type.", TenantSettingsErrorCodes.InvalidValue);
        }
    }

    private async Task<SettingsChangeResult> ValidateCurrencyChangeAsync(
        Guid tenantId,
        string newCurrency,
        CancellationToken cancellationToken = default)
    {
        var currency = newCurrency.Trim().ToUpperInvariant();
        if (!SupportedCurrencies.Contains(currency))
        {
            return SettingsChangeResult.Fail(
                $"Currency {currency} is not supported for RKSV-compliant systems.",
                TenantSettingsErrorCodes.CurrencyNotRksvCompatible);
        }

        string? warning = null;
        var hasInvoices = await HasInvoicesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (hasInvoices)
        {
            warning =
                "Tenant has existing invoices. Currency change will not affect historical invoices. " +
                "New invoices will use the new currency.";
        }
        else
        {
            var hasPayments = await HasSignedFiscalPaymentsAsync(tenantId, cancellationToken).ConfigureAwait(false);
            if (hasPayments)
            {
                warning =
                    "Tenant has existing fiscal payments. Currency change will not affect historical receipt data.";
            }
        }

        return SettingsChangeResult.Success(Guid.Empty, warning);
    }

    private async Task<SettingsChangeResult> ValidateCountryChangeAsync(
        Guid tenantId,
        string newCountry,
        CancellationToken cancellationToken = default)
    {
        var country = newCountry.Trim().ToUpperInvariant();
        if (country.Length != 2 || !SupportedCountries.Contains(country))
        {
            return SettingsChangeResult.Fail(
                $"Country {country} is not RKSV-compatible.",
                TenantSettingsErrorCodes.CountryNotRksvCompatible);
        }

        var hasFiscalData = await HasSignedFiscalPaymentsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (hasFiscalData)
        {
            var settings = await GetCompanySettingsAsync(tenantId, track: false, cancellationToken)
                .ConfigureAwait(false);
            var currentCountry = (settings?.Country ?? string.Empty).Trim().ToUpperInvariant();
            if (!string.Equals(currentCountry, country, StringComparison.OrdinalIgnoreCase))
            {
                return SettingsChangeResult.Fail(
                    "Cannot change country after fiscal data exists. " +
                    "This would affect RKSV compliance. " +
                    "Please contact support for assistance.",
                    TenantSettingsErrorCodes.CountryLockedFiscal);
            }
        }

        return SettingsChangeResult.Success(Guid.Empty);
    }

    private static SettingsChangeResult ValidateTimezoneChange(string newTimezone)
    {
        var tz = newTimezone.Trim();
        if (tz.Length is < 3 or > 50)
        {
            return SettingsChangeResult.Fail(
                "Timezone must be between 3 and 50 characters.",
                TenantSettingsErrorCodes.InvalidValue);
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(tz);
        }
        catch (TimeZoneNotFoundException)
        {
            // Allow common IANA ids that Windows may not resolve without ICU mapping.
            if (!tz.Contains('/'))
            {
                return SettingsChangeResult.Fail(
                    $"Timezone {tz} is not recognized.",
                    TenantSettingsErrorCodes.InvalidValue);
            }
        }
        catch (InvalidTimeZoneException)
        {
            return SettingsChangeResult.Fail(
                $"Timezone {tz} is invalid.",
                TenantSettingsErrorCodes.InvalidValue);
        }

        return SettingsChangeResult.Success(Guid.Empty);
    }

    private async Task<SettingsChangeResult> ValidateFiscalChangeAsync(
        Guid tenantId,
        FiscalSettingsValueDto fiscal,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fiscal.CompanyName) || fiscal.CompanyName.Trim().Length > 100)
            return SettingsChangeResult.Fail("Company name is required (max 100).", TenantSettingsErrorCodes.InvalidValue);
        if (string.IsNullOrWhiteSpace(fiscal.CompanyAddress) || fiscal.CompanyAddress.Trim().Length > 200)
            return SettingsChangeResult.Fail("Company address is required (max 200).", TenantSettingsErrorCodes.InvalidValue);

        var tax = (fiscal.CompanyTaxNumber ?? string.Empty).Trim().ToUpperInvariant();
        if (!TaxNumberRegex.IsMatch(tax))
        {
            return SettingsChangeResult.Fail(
                "Company tax number must match ATU########.",
                TenantSettingsErrorCodes.InvalidValue);
        }

        var hasPayments = await HasSignedFiscalPaymentsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (hasPayments)
        {
            return SettingsChangeResult.Success(
                Guid.Empty,
                "Tenant has existing fiscal payments. Historical receipt snapshots will not be rewritten.");
        }

        return SettingsChangeResult.Success(Guid.Empty);
    }

    /// <summary>True when the tenant has at least one TSE-signed payment (RKSV fiscal footprint).</summary>
    private async Task<bool> HasSignedFiscalPaymentsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await (
                from p in _db.PaymentDetails.AsNoTracking().IgnoreQueryFilters()
                join cr in _db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
                    on p.CashRegisterId equals cr.Id
                where cr.TenantId == tenantId
                      && p.TseSignature != null
                      && p.TseSignature != ""
                select p.Id)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> HasInvoicesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Invoices.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(i => i.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static object GetCurrentSettingValue(CompanySettings settings, TenantSettingType settingType) =>
        settingType switch
        {
            TenantSettingType.Currency => settings.Currency,
            TenantSettingType.Country => settings.Country,
            TenantSettingType.Timezone => settings.TimeZone,
            TenantSettingType.FiscalSettings => MapFiscal(settings),
            _ => throw new ArgumentOutOfRangeException(nameof(settingType)),
        };

    private static FiscalSettingsValueDto MapFiscal(CompanySettings settings) =>
        new()
        {
            CompanyName = settings.CompanyName,
            CompanyAddress = settings.CompanyAddress,
            CompanyTaxNumber = settings.CompanyTaxNumber,
            CompanyVatNumber = settings.CompanyVatNumber,
            CompanyRegistrationNumber = settings.CompanyRegistrationNumber,
        };

    private static void ApplyFiscal(CompanySettings settings, FiscalSettingsValueDto fiscal)
    {
        settings.CompanyName = fiscal.CompanyName.Trim();
        settings.CompanyAddress = fiscal.CompanyAddress.Trim();
        settings.CompanyTaxNumber = fiscal.CompanyTaxNumber.Trim().ToUpperInvariant();
        settings.CompanyVatNumber = string.IsNullOrWhiteSpace(fiscal.CompanyVatNumber)
            ? null
            : fiscal.CompanyVatNumber.Trim();
        settings.CompanyRegistrationNumber = string.IsNullOrWhiteSpace(fiscal.CompanyRegistrationNumber)
            ? null
            : fiscal.CompanyRegistrationNumber.Trim();
    }

    private static (object? Value, string? Error) NormalizeNewValue(TenantSettingType settingType, JsonElement newValue)
    {
        try
        {
            return settingType switch
            {
                TenantSettingType.Currency => (ExtractScalarString(newValue)?.ToUpperInvariant(), null),
                TenantSettingType.Country => (ExtractScalarString(newValue)?.ToUpperInvariant(), null),
                TenantSettingType.Timezone => (ExtractScalarString(newValue), null),
                TenantSettingType.FiscalSettings => NormalizeFiscal(newValue),
                _ => (null, "Unknown setting type."),
            };
        }
        catch (JsonException ex)
        {
            return (null, $"Invalid JSON value: {ex.Message}");
        }
    }

    private static (object? Value, string? Error) NormalizeFiscal(JsonElement newValue)
    {
        if (newValue.ValueKind != JsonValueKind.Object)
            return (null, "Fiscal settings must be a JSON object.");

        var fiscal = JsonSerializer.Deserialize<FiscalSettingsValueDto>(newValue.GetRawText(), JsonOptions);
        if (fiscal is null)
            return (null, "Fiscal settings could not be parsed.");

        fiscal.CompanyName = fiscal.CompanyName?.Trim() ?? string.Empty;
        fiscal.CompanyAddress = fiscal.CompanyAddress?.Trim() ?? string.Empty;
        fiscal.CompanyTaxNumber = (fiscal.CompanyTaxNumber ?? string.Empty).Trim().ToUpperInvariant();
        return (fiscal, null);
    }

    private static string? ExtractScalarString(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString()?.Trim();

        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("value", out var valueProp)
            && valueProp.ValueKind == JsonValueKind.String)
        {
            return valueProp.GetString()?.Trim();
        }

        return null;
    }

    private static string? DeserializeScalarString(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        using var doc = JsonDocument.Parse(json);
        return ExtractScalarString(doc.RootElement) ?? doc.RootElement.GetRawText().Trim('"');
    }

    private static bool AreEqual(object current, object proposed)
    {
        var left = JsonSerializer.Serialize(current, JsonOptions);
        var right = JsonSerializer.Serialize(proposed, JsonOptions);
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static object? SafeDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch
        {
            return json;
        }
    }

    private static string RolesOrUnknown() => Authorization.Roles.SuperAdmin;
}
