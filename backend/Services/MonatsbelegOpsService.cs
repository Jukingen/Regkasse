using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <inheritdoc />
public sealed class MonatsbelegOpsService : IMonatsbelegOpsService
{
    public const string AlreadyNotifiedCode = "ALREADY_NOTIFIED";
    public const string NotMissingCode = "NOT_MISSING";
    public const string RegisterNotFoundCode = "REGISTER_NOT_FOUND";
    public const string TenantContextRequiredCode = "TENANT_CONTEXT_REQUIRED";
    public const string WindowClosedReason = "window_closed";

    private readonly AppDbContext _db;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IRksvMonatsbelegPolicy _monatsbelegPolicy;
    private readonly IRksvSpecialReceiptService _specialReceipts;
    private readonly IActivityEventService _activity;
    private readonly IAuditLogService _audit;
    private readonly IOptionsMonitor<MonatsbelegOpsOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<MonatsbelegOpsService> _logger;

    public MonatsbelegOpsService(
        AppDbContext db,
        ISettingsTenantResolver tenantResolver,
        ICurrentTenantAccessor tenantAccessor,
        IRksvMonatsbelegPolicy monatsbelegPolicy,
        IRksvSpecialReceiptService specialReceipts,
        IActivityEventService activity,
        IAuditLogService audit,
        IOptionsMonitor<MonatsbelegOpsOptions> options,
        TimeProvider time,
        ILogger<MonatsbelegOpsService> logger)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _tenantAccessor = tenantAccessor;
        _monatsbelegPolicy = monatsbelegPolicy;
        _specialReceipts = specialReceipts;
        _activity = activity;
        _audit = audit;
        _options = options;
        _time = time;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NotifyMonatsbelegManagerResult> NotifyManagerAsync(
        string actorUserId,
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId == Guid.Empty)
        {
            return Fail(TenantContextRequiredCode, "Tenant context is required.");
        }

        var register = await _db.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId && r.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (register is null)
            return Fail(RegisterNotFoundCode, "Cash register was not found.");

        var decision = await _monatsbelegPolicy.EvaluateSalesGateAsync(cashRegisterId, cancellationToken)
            .ConfigureAwait(false);
        if (!decision.PreviousMonthMissing)
        {
            return new NotifyMonatsbelegManagerResult
            {
                Ok = true,
                Code = NotMissingCode,
                Message = "Monatsbeleg is already present for the previous month.",
            };
        }

        var viennaNow = ViennaNow();
        var prev = new DateTime(viennaNow.Year, viennaNow.Month, 1).AddMonths(-1);
        var dedupKey = $"monatsbeleg-manager-contacted:{cashRegisterId:D}:{prev:yyyy-MM}:{viennaNow:yyyy-MM-dd}";

        var already = await _db.ActivityEvents.AsNoTracking()
            .AnyAsync(e => e.TenantId == tenantId && e.DedupKey == dedupKey, cancellationToken)
            .ConfigureAwait(false);
        if (already)
        {
            return new NotifyMonatsbelegManagerResult
            {
                Ok = true,
                Code = AlreadyNotifiedCode,
                Message = "Mandanten-Admin was already notified today.",
            };
        }

        var label = FormatRegisterLabel(register.RegisterNumber, register.Location);
        await _activity.PublishAsync(
                new ActivityEventPublishRequest(
                    tenantId,
                    ActivityEventType.MonatsbelegManagerContacted,
                    Title: $"Monatsbeleg fehlt — {label}",
                    Description:
                        $"Kassierer hat den Mandanten-Admin kontaktiert: Monatsbeleg für {prev:yyyy-MM} fehlt an Kasse {label}.",
                    DedupKey: dedupKey,
                    ActorUserId: actorUserId,
                    EntityType: "cash_register",
                    EntityId: cashRegisterId.ToString("D"),
                    Metadata: new Dictionary<string, object>
                    {
                        ["cashRegisterId"] = cashRegisterId.ToString("D"),
                        ["registerNumber"] = register.RegisterNumber ?? "",
                        ["period"] = prev.ToString("yyyy-MM"),
                    }),
                cancellationToken)
            .ConfigureAwait(false);

        await _audit.LogSystemOperationAsync(
            "MonatsbelegManagerContacted",
            "cash_register",
            actorUserId,
            Roles.FallbackUnknown,
            description: $"Cashier requested Manager for missing Monatsbeleg {prev:yyyy-MM} on {register.RegisterNumber}",
            actionType: AuditEventType.MonatsbelegManagerContacted,
            entityId: cashRegisterId,
            tenantId: tenantId,
            requestData: new { cashRegisterId, period = prev.ToString("yyyy-MM") });

        return new NotifyMonatsbelegManagerResult
        {
            Ok = true,
            Code = "OK",
            Message = "Mandanten-Admin was notified.",
        };
    }

    /// <inheritdoc />
    public async Task<int> PublishMissingRemindersAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.CurrentValue.ReminderEnabled)
            return 0;

        var previous = _tenantAccessor.TenantId;
        _tenantAccessor.TenantId = null;

        try
        {
            var viennaNow = ViennaNow();
            var prev = new DateTime(viennaNow.Year, viennaNow.Month, 1).AddMonths(-1);
            var tenants = await _db.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t => t.DeletedAtUtc == null && t.Status == TenantStatuses.Active)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var sent = 0;
            foreach (var tenant in tenants)
            {
                try
                {
                    sent += await PublishTenantRemindersAsync(
                            tenant.Id,
                            tenant.Name,
                            viennaNow,
                            prev,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Monatsbeleg reminder failed for tenant {TenantId}", tenant.Id);
                }
            }

            return sent;
        }
        finally
        {
            _tenantAccessor.TenantId = previous;
        }
    }

    /// <inheritdoc />
    public async Task<int> RunAutoCreateAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.CurrentValue.AutoCreateEnabled)
            return 0;

        var viennaNow = ViennaNow();
        var catchUp = Math.Clamp(_options.CurrentValue.CatchUpThroughDay, 1, 31);
        if (!AutoMonatsbelegCutoff.IsInAutoCreateWindow(viennaNow, catchUp))
        {
            await RunMissedAlertsAsync(viennaNow, catchUp, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        var previous = _tenantAccessor.TenantId;
        _tenantAccessor.TenantId = null;

        try
        {
            var tenantIds = await _db.CompanySettings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.AutoMonatsbelegEnabled)
                .Select(s => s.TenantId)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var created = 0;
            foreach (var tenantId in tenantIds)
            {
                try
                {
                    created += await AutoCreateForTenantAsync(tenantId, viennaNow, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Auto-Monatsbeleg failed for tenant {TenantId}", tenantId);
                }
            }

            return created;
        }
        finally
        {
            _tenantAccessor.TenantId = previous;
        }
    }

    private async Task<int> PublishTenantRemindersAsync(
        Guid tenantId,
        string? tenantName,
        DateTime viennaNow,
        DateTime prevMonthAnchor,
        CancellationToken cancellationToken)
    {
        var registers = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r =>
                r.TenantId == tenantId
                && r.IsActive
                && r.Status != RegisterStatus.Decommissioned
                && r.Status != RegisterStatus.Disabled)
            .Select(r => new RegisterHint(r.Id, r.RegisterNumber, r.Location))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ambient = _tenantAccessor.TenantId;
        _tenantAccessor.TenantId = tenantId;
        var sent = 0;
        try
        {
            foreach (var register in registers)
            {
                var missing = !await _monatsbelegPolicy
                    .HasMonatsbelegForRegisterMonthAsync(
                        register.Id, prevMonthAnchor.Year, prevMonthAnchor.Month, cancellationToken)
                    .ConfigureAwait(false);
                if (!missing)
                    continue;

                var dedupKey =
                    $"monatsbeleg-missing:{register.Id:D}:{prevMonthAnchor:yyyy-MM}:{viennaNow:yyyy-MM-dd}";
                var already = await _db.ActivityEvents
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .AnyAsync(e => e.TenantId == tenantId && e.DedupKey == dedupKey, cancellationToken)
                    .ConfigureAwait(false);
                if (already)
                    continue;

                var label = FormatRegisterLabel(register.RegisterNumber, register.Location);
                await _activity.PublishAsync(
                        new ActivityEventPublishRequest(
                            tenantId,
                            ActivityEventType.MonatsbelegMissingReminder,
                            Title: $"Monatsbeleg fehlt — {label}",
                            Description:
                                $"Der Monatsbeleg für {prevMonthAnchor:yyyy-MM} fehlt an Kasse {label} " +
                                $"(Mandant {tenantName ?? tenantId.ToString("D")}). Bitte in FA erstellen.",
                            DedupKey: dedupKey,
                            EntityType: "cash_register",
                            EntityId: register.Id.ToString("D"),
                            Metadata: new Dictionary<string, object>
                            {
                                ["cashRegisterId"] = register.Id.ToString("D"),
                                ["registerNumber"] = register.RegisterNumber ?? "",
                                ["period"] = prevMonthAnchor.ToString("yyyy-MM"),
                            }),
                        cancellationToken)
                    .ConfigureAwait(false);
                sent++;
            }

            sent += await PublishJahresbelegFonRemindersForTenantAsync(
                    tenantId,
                    tenantName,
                    viennaNow,
                    registers,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _tenantAccessor.TenantId = ambient;
        }

        return sent;
    }

    private async Task<int> PublishJahresbelegFonRemindersForTenantAsync(
        Guid tenantId,
        string? tenantName,
        DateTime viennaNow,
        IReadOnlyList<RegisterHint> registers,
        CancellationToken cancellationToken)
    {
        var priorYear = viennaNow.Year - 1;
        var deadline = AutoMonatsbelegCutoff.JahresbelegFonDeadline(priorYear);
        var sent = 0;
        foreach (var register in registers)
        {
            var paymentId = await _db.PaymentDetails.AsNoTracking()
                .Where(p =>
                    p.CashRegisterId == register.Id
                    && p.IsActive
                    && (
                        (p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Jahresbeleg
                         && p.RksvSpecialReceiptYear == priorYear)
                        || (p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg
                            && p.RksvSpecialReceiptYear == priorYear
                            && p.RksvSpecialReceiptMonth == 12)))
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (paymentId is null)
                continue;

            var fonStatus = await _db.RksvSpecialReceiptFinanzOnlineSubmissions.AsNoTracking()
                .Where(s => s.PaymentId == paymentId.Value)
                .Select(s => s.Status)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (string.Equals(fonStatus, RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Verified, StringComparison.OrdinalIgnoreCase))
                continue;

            var overdue = viennaNow.Date > deadline;
            var dedupKey = $"jahresbeleg-fon:{register.Id:D}:{priorYear}:{viennaNow:yyyy-MM-dd}";
            var already = await _db.ActivityEvents
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(e => e.TenantId == tenantId && e.DedupKey == dedupKey, cancellationToken)
                .ConfigureAwait(false);
            if (already)
                continue;

            var label = FormatRegisterLabel(register.RegisterNumber, register.Location);
            var days = Math.Max(0, (deadline - viennaNow.Date).Days);
            await _activity.PublishAsync(
                    new ActivityEventPublishRequest(
                        tenantId,
                        ActivityEventType.JahresbelegFonReminder,
                        Title: overdue
                            ? $"Jahresbeleg FinanzOnline überfällig — {label}"
                            : $"Jahresbeleg FinanzOnline bis 15.02. — {label}",
                        Description: overdue
                            ? $"Jahresbeleg {priorYear} an Kasse {label} (Mandant {tenantName ?? tenantId.ToString("D")}) ist nicht an FinanzOnline übermittelt (Frist 15. Februar)."
                            : $"Jahresbeleg {priorYear} an Kasse {label} muss bis 15. Februar an FinanzOnline übermittelt werden ({days} Tag(e) verbleibend).",
                        DedupKey: dedupKey,
                        EntityType: "cash_register",
                        EntityId: register.Id.ToString("D"),
                        Metadata: new Dictionary<string, object>
                        {
                            ["cashRegisterId"] = register.Id.ToString("D"),
                            ["registerNumber"] = register.RegisterNumber ?? "",
                            ["period"] = $"{priorYear}-12",
                            ["fonStatus"] = fonStatus ?? RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Pending,
                            ["overdue"] = overdue,
                        }),
                    cancellationToken)
                .ConfigureAwait(false);
            sent++;
        }

        return sent;
    }

    private async Task<int> AutoCreateForTenantAsync(
        Guid tenantId,
        DateTime viennaNow,
        CancellationToken cancellationToken)
    {
        var tenantActive = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                t => t.Id == tenantId && t.DeletedAtUtc == null && t.Status == TenantStatuses.Active,
                cancellationToken)
            .ConfigureAwait(false);
        if (!tenantActive)
            return 0;

        var retryCount = await _db.CompanySettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.MonatsbelegRetryCount)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        retryCount = AutoMonatsbelegCutoff.ClampRetryCount(
            retryCount <= 0 ? AutoMonatsbelegCutoff.DefaultRetryCount : retryCount);

        var registers = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r =>
                r.TenantId == tenantId
                && r.IsActive
                && r.Status != RegisterStatus.Decommissioned
                && r.Status != RegisterStatus.Disabled)
            .Select(r => new { r.Id, r.RegisterNumber, r.Location })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var prev = AutoMonatsbelegCutoff.PreviousMonthAnchor(viennaNow);
        var ambient = _tenantAccessor.TenantId;
        _tenantAccessor.TenantId = tenantId;
        var created = 0;
        try
        {
            foreach (var register in registers)
            {
                var createdNow = await AutoCreateForRegisterAsync(
                        tenantId,
                        register.Id,
                        register.RegisterNumber,
                        register.Location,
                        prev.Year,
                        prev.Month,
                        retryCount,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (createdNow)
                    created++;
            }
        }
        finally
        {
            _tenantAccessor.TenantId = ambient;
        }

        return created;
    }

    private async Task<bool> AutoCreateForRegisterAsync(
        Guid tenantId,
        Guid registerId,
        string? registerNumber,
        string? location,
        int year,
        int month,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var alreadyPresent = await _monatsbelegPolicy
            .HasMonatsbelegForRegisterMonthAsync(registerId, year, month, cancellationToken)
            .ConfigureAwait(false);

        var run = await _db.MonatsbelegAutoRuns
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId
                    && r.CashRegisterId == registerId
                    && r.Year == year
                    && r.Month == month,
                cancellationToken)
            .ConfigureAwait(false);

        if (alreadyPresent)
        {
            if (run is { Status: not MonatsbelegAutoRunStatuses.Succeeded })
            {
                run.Status = MonatsbelegAutoRunStatuses.Succeeded;
                run.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return false;
        }

        if (run != null && run.Status == MonatsbelegAutoRunStatuses.Exhausted)
            return false;

        if (run == null)
        {
            run = new MonatsbelegAutoRun
            {
                TenantId = tenantId,
                CashRegisterId = registerId,
                Year = year,
                Month = month,
                Status = MonatsbelegAutoRunStatuses.Pending,
                CorrelationId = Guid.NewGuid().ToString("D"),
                CreatedAtUtc = _time.GetUtcNow().UtcDateTime,
                UpdatedAtUtc = _time.GetUtcNow().UtcDateTime,
            };
            _db.MonatsbelegAutoRuns.Add(run);
        }

        if (string.IsNullOrWhiteSpace(run.CorrelationId))
            run.CorrelationId = Guid.NewGuid().ToString("D");

        var label = FormatRegisterLabel(registerNumber, location);
        var period = $"{year:D4}-{month:D2}";
        Exception? lastException = null;

        while (run.AttemptCount < retryCount)
        {
            run.AttemptCount++;
            run.LastAttemptUtc = _time.GetUtcNow().UtcDateTime;
            run.UpdatedAtUtc = run.LastAttemptUtc.Value;
            run.Status = MonatsbelegAutoRunStatuses.Pending;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var response = await _specialReceipts.CreateMonatsbelegAsync(
                        new CreateMonatsbelegRequest
                        {
                            CashRegisterId = registerId,
                            Year = year,
                            Month = month,
                            Reason = "Auto-Monatsbeleg",
                        },
                        AutoMonatsbelegCutoff.SystemActorUserId,
                        forcePastMonth: true,
                        cancellationToken)
                    .ConfigureAwait(false);

                run.Status = MonatsbelegAutoRunStatuses.Succeeded;
                run.PaymentId = response.PaymentId;
                run.LastError = null;
                run.NextRetryUtc = null;
                run.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await PublishCreatedAsync(
                        tenantId,
                        registerId,
                        registerNumber,
                        label,
                        period,
                        response,
                        run.CorrelationId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
            catch (RksvOperationGuardException ex)
                when (ex.ErrorCode == RksvGuardErrorCodes.DuplicateMonatsbeleg)
            {
                run.Status = MonatsbelegAutoRunStatuses.Succeeded;
                run.LastError = null;
                run.NextRetryUtc = null;
                run.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                run.LastError = TruncateError(ex.Message);
                run.Status = run.AttemptCount >= retryCount
                    ? MonatsbelegAutoRunStatuses.Exhausted
                    : MonatsbelegAutoRunStatuses.Failed;
                run.NextRetryUtc = run.Status == MonatsbelegAutoRunStatuses.Failed
                    ? _time.GetUtcNow().UtcDateTime + AutoMonatsbelegCutoff.RetryBackoff(run.AttemptCount)
                    : null;
                run.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogWarning(
                    ex,
                    "Auto-Monatsbeleg attempt {Attempt}/{RetryCount} failed for register {RegisterId} tenant {TenantId} period {Period}",
                    run.AttemptCount,
                    retryCount,
                    registerId,
                    tenantId,
                    period);

                if (run.AttemptCount >= retryCount)
                    break;

                if (_options.CurrentValue.RetryBackoffEnabled)
                    await Task.Delay(AutoMonatsbelegCutoff.RetryBackoff(run.AttemptCount), cancellationToken)
                        .ConfigureAwait(false);
            }
        }

        await PublishFailedAsync(
                tenantId,
                registerId,
                registerNumber,
                label,
                period,
                run.CorrelationId,
                lastException?.Message,
                cancellationToken)
            .ConfigureAwait(false);
        return false;
    }

    private async Task RunMissedAlertsAsync(
        DateTime viennaNow,
        int catchUp,
        CancellationToken cancellationToken)
    {
        var previous = _tenantAccessor.TenantId;
        _tenantAccessor.TenantId = null;
        try
        {
            var tenantIds = await _db.CompanySettings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.AutoMonatsbelegEnabled)
                .Select(s => s.TenantId)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var tenantId in tenantIds)
            {
                try
                {
                    await EmitMissedForTenantAsync(tenantId, viennaNow, catchUp, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Auto-Monatsbeleg miss alert failed for tenant {TenantId}", tenantId);
                }
            }
        }
        finally
        {
            _tenantAccessor.TenantId = previous;
        }
    }

    private async Task EmitMissedForTenantAsync(
        Guid tenantId,
        DateTime viennaNow,
        int catchUp,
        CancellationToken cancellationToken)
    {
        var tenantActive = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                t => t.Id == tenantId && t.DeletedAtUtc == null && t.Status == TenantStatuses.Active,
                cancellationToken)
            .ConfigureAwait(false);
        if (!tenantActive)
            return;

        var registers = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r =>
                r.TenantId == tenantId
                && r.IsActive
                && r.Status != RegisterStatus.Decommissioned
                && r.Status != RegisterStatus.Disabled)
            .Select(r => new { r.Id, r.RegisterNumber, r.Location })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var prev = AutoMonatsbelegCutoff.PreviousMonthAnchor(viennaNow);
        var period = $"{prev.Year:D4}-{prev.Month:D2}";
        var ambient = _tenantAccessor.TenantId;
        _tenantAccessor.TenantId = tenantId;
        try
        {
            foreach (var register in registers)
            {
                var present = await _monatsbelegPolicy
                    .HasMonatsbelegForRegisterMonthAsync(register.Id, prev.Year, prev.Month, cancellationToken)
                    .ConfigureAwait(false);
                if (present)
                    continue;

                await PersistMissedRunAsync(tenantId, register.Id, prev.Year, prev.Month, cancellationToken)
                    .ConfigureAwait(false);

                await PublishMissedAsync(
                        tenantId,
                        register.Id,
                        register.RegisterNumber,
                        FormatRegisterLabel(register.RegisterNumber, register.Location),
                        period,
                        viennaNow.Day,
                        catchUp,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            _tenantAccessor.TenantId = ambient;
        }
    }

    private async Task PersistMissedRunAsync(
        Guid tenantId,
        Guid registerId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var run = await _db.MonatsbelegAutoRuns
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId
                    && r.CashRegisterId == registerId
                    && r.Year == year
                    && r.Month == month,
                cancellationToken)
            .ConfigureAwait(false);

        if (run == null)
        {
            _db.MonatsbelegAutoRuns.Add(new MonatsbelegAutoRun
            {
                TenantId = tenantId,
                CashRegisterId = registerId,
                Year = year,
                Month = month,
                Status = MonatsbelegAutoRunStatuses.Exhausted,
                LastError = WindowClosedReason,
                CorrelationId = Guid.NewGuid().ToString("D"),
                CreatedAtUtc = _time.GetUtcNow().UtcDateTime,
                UpdatedAtUtc = _time.GetUtcNow().UtcDateTime,
            });
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (run.Status == MonatsbelegAutoRunStatuses.Succeeded)
            return;

        if (string.IsNullOrWhiteSpace(run.LastError))
            run.LastError = WindowClosedReason;
        if (run.Status != MonatsbelegAutoRunStatuses.Exhausted)
            run.Status = MonatsbelegAutoRunStatuses.Exhausted;
        run.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishMissedAsync(
        Guid tenantId,
        Guid registerId,
        string? registerNumber,
        string label,
        string period,
        int viennaDayOfMonth,
        int catchUp,
        CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString("D");
        await _activity.PublishAsync(
                new ActivityEventPublishRequest(
                    tenantId,
                    ActivityEventType.MonatsbelegAutoCreateMissed,
                    Title: $"Monatsbeleg automatisch verpasst — {label}",
                    Description:
                        $"Auto-Monatsbeleg-Fenster (Tag 1–{catchUp}) ist vorbei. " +
                        $"Monatsbeleg {period} fehlt an Kasse {label}. Bitte in FA mit force erstellen.",
                    DedupKey: $"monatsbeleg-auto-missed:{registerId:D}:{period}",
                    ActorUserId: AutoMonatsbelegCutoff.SystemActorUserId,
                    EntityType: "cash_register",
                    EntityId: registerId.ToString("D"),
                    Metadata: new Dictionary<string, object>
                    {
                        ["tenant_id"] = tenantId.ToString("D"),
                        ["cashRegisterId"] = registerId.ToString("D"),
                        ["register_id"] = registerId.ToString("D"),
                        ["registerNumber"] = registerNumber ?? "",
                        ["period"] = period,
                        ["viennaDayOfMonth"] = viennaDayOfMonth,
                        ["catchUpThroughDay"] = catchUp,
                        ["reason"] = WindowClosedReason,
                    }),
                cancellationToken)
            .ConfigureAwait(false);

        await _audit.LogSystemOperationAsync(
            "MONATSBELEG_AUTO_CREATE_MISSED",
            "cash_register",
            AutoMonatsbelegCutoff.SystemActorUserId,
            AutoMonatsbelegCutoff.SystemActorRole,
            description: $"Automatic Monatsbeleg missed for {period} on {registerNumber}",
            status: AuditLogStatus.Failed,
            actionType: AuditEventType.MonatsbelegAutoCreateMissed,
            entityId: registerId,
            tenantId: tenantId,
            requestData: new
            {
                actor_user_id = AutoMonatsbelegCutoff.SystemActorUserId,
                tenant_id = tenantId,
                cash_register_id = registerId,
                period,
                vienna_day_of_month = viennaDayOfMonth,
                catch_up_through_day = catchUp,
                reason = WindowClosedReason,
                timestamp_utc = _time.GetUtcNow().UtcDateTime,
                correlation_id = correlationId,
            });
    }

    private async Task PublishCreatedAsync(
        Guid tenantId,
        Guid registerId,
        string? registerNumber,
        string label,
        string period,
        CreateMonatsbelegResponse response,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var tseMasked = AutoMonatsbelegCutoff.MaskTseSignature(response.QrData);
        var metadata = new Dictionary<string, object>
        {
            ["tenant_id"] = tenantId.ToString("D"),
            ["register_id"] = registerId.ToString("D"),
            ["cashRegisterId"] = registerId.ToString("D"),
            ["registerNumber"] = registerNumber ?? "",
            ["period"] = period,
            ["amount"] = 0m,
            ["tse_signature"] = tseMasked,
            ["correlation_id"] = correlationId,
            ["paymentId"] = response.PaymentId.ToString("D"),
        };

        var createdKey = $"monatsbeleg-created:{registerId:D}:{period}";
        await _activity.PublishAsync(
                new ActivityEventPublishRequest(
                    tenantId,
                    ActivityEventType.MonatsbelegCreated,
                    Title: $"Monatsbeleg erstellt — {label}",
                    Description: $"Monatsbeleg für {period} wurde für Kasse {label} automatisch erstellt.",
                    DedupKey: createdKey,
                    ActorUserId: AutoMonatsbelegCutoff.SystemActorUserId,
                    EntityType: "cash_register",
                    EntityId: registerId.ToString("D"),
                    Metadata: metadata),
                cancellationToken)
            .ConfigureAwait(false);

        var autoKey = $"monatsbeleg-auto-created:{registerId:D}:{period}";
        await _activity.PublishAsync(
                new ActivityEventPublishRequest(
                    tenantId,
                    ActivityEventType.MonatsbelegAutoCreated,
                    Title: $"Monatsbeleg automatisch erstellt — {label}",
                    Description: $"Auto-Monatsbeleg für {period} wurde für Kasse {label} erstellt.",
                    DedupKey: autoKey,
                    ActorUserId: AutoMonatsbelegCutoff.SystemActorUserId,
                    EntityType: "cash_register",
                    EntityId: registerId.ToString("D"),
                    Metadata: metadata),
                cancellationToken)
            .ConfigureAwait(false);

        await _audit.LogSystemOperationAsync(
            "MONATSBELEG_CREATED",
            "payment_details",
            AutoMonatsbelegCutoff.SystemActorUserId,
            AutoMonatsbelegCutoff.SystemActorRole,
            description: $"Automatic Monatsbeleg created for {period} on {registerNumber}",
            actionType: AuditEventType.MonatsbelegCreated,
            entityId: response.PaymentId,
            tenantId: tenantId,
            requestData: new
            {
                actor_user_id = AutoMonatsbelegCutoff.SystemActorUserId,
                tenant_id = tenantId,
                cash_register_id = registerId,
                period,
                tse_signature = tseMasked,
                timestamp_utc = _time.GetUtcNow().UtcDateTime,
                correlation_id = correlationId,
            });
    }

    private async Task PublishFailedAsync(
        Guid tenantId,
        Guid registerId,
        string? registerNumber,
        string label,
        string period,
        string correlationId,
        string? error,
        CancellationToken cancellationToken)
    {
        var dedupKey = $"monatsbeleg-auto-failed:{registerId:D}:{period}";
        await _activity.PublishAsync(
                new ActivityEventPublishRequest(
                    tenantId,
                    ActivityEventType.MonatsbelegAutoCreateFailed,
                    Title: $"Monatsbeleg automatisch fehlgeschlagen — {label}",
                    Description:
                        $"Auto-Monatsbeleg für {period} an Kasse {label} ist nach Wiederholungen fehlgeschlagen. Bitte in FA manuell erstellen.",
                    DedupKey: dedupKey,
                    ActorUserId: AutoMonatsbelegCutoff.SystemActorUserId,
                    EntityType: "cash_register",
                    EntityId: registerId.ToString("D"),
                    Metadata: new Dictionary<string, object>
                    {
                        ["tenant_id"] = tenantId.ToString("D"),
                        ["register_id"] = registerId.ToString("D"),
                        ["cashRegisterId"] = registerId.ToString("D"),
                        ["registerNumber"] = registerNumber ?? "",
                        ["period"] = period,
                        ["correlation_id"] = correlationId,
                        ["error"] = TruncateError(error) ?? "",
                    }),
                cancellationToken)
            .ConfigureAwait(false);

        await _audit.LogSystemOperationAsync(
            "MONATSBELEG_AUTO_CREATE_FAILED",
            "cash_register",
            AutoMonatsbelegCutoff.SystemActorUserId,
            AutoMonatsbelegCutoff.SystemActorRole,
            description: $"Automatic Monatsbeleg failed for {period} on {registerNumber}",
            status: AuditLogStatus.Failed,
            errorDetails: TruncateError(error),
            actionType: AuditEventType.MonatsbelegAutoCreateFailed,
            entityId: registerId,
            tenantId: tenantId,
            requestData: new
            {
                actor_user_id = AutoMonatsbelegCutoff.SystemActorUserId,
                tenant_id = tenantId,
                cash_register_id = registerId,
                period,
                timestamp_utc = _time.GetUtcNow().UtcDateTime,
                correlation_id = correlationId,
            });
    }

    private static string? TruncateError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;
        return message.Length <= 500 ? message : message[..500];
    }

    private DateTime ViennaNow()
    {
        var utc = _time.GetUtcNow().UtcDateTime;
        return TimeZoneInfo.ConvertTimeFromUtc(utc, PostgreSqlUtcDateTime.AustriaTimeZone);
    }

    private readonly record struct RegisterHint(Guid Id, string? RegisterNumber, string? Location);

    private static NotifyMonatsbelegManagerResult Fail(string code, string message) =>
        new() { Ok = false, Code = code, Message = message };

    private static string FormatRegisterLabel(string? registerNumber, string? location)
    {
        var number = registerNumber?.Trim();
        var place = location?.Trim();
        if (!string.IsNullOrEmpty(number) && !string.IsNullOrEmpty(place))
            return $"{number} — {place}";
        return number ?? place ?? "Kasse";
    }
}
