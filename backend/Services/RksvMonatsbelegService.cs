using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <inheritdoc />
public sealed class RksvMonatsbelegService : IRksvMonatsbelegService
{
    private readonly AppDbContext _db;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly TimeProvider _time;

    public RksvMonatsbelegService(
        AppDbContext db,
        ISettingsTenantResolver tenantResolver,
        TimeProvider time)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _time = time;
    }

    /// <inheritdoc />
    public async Task<MonatsbelegListResponse> ListAsync(
        MonatsbelegListQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId == Guid.Empty)
        {
            return new MonatsbelegListResponse
            {
                Year = query.Year ?? AutoMonatsbelegCutoff.ToVienna(_time.GetUtcNow().UtcDateTime).Year,
            };
        }

        var viennaYear = AutoMonatsbelegCutoff.ToVienna(_time.GetUtcNow().UtcDateTime).Year;
        var year = query.Year is >= 2000 and <= 2100 ? query.Year.Value : viennaYear;
        var status = NormalizeStatus(query.Status);
        var page = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 200);

        var registers = await _db.CashRegisters.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .Select(r => new { r.Id, r.RegisterNumber, r.Location })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var registerById = registers.ToDictionary(r => r.Id);

        var registerIds = registers.Select(r => r.Id).ToList();
        var payments = await _db.PaymentDetails.AsNoTracking()
            .Where(p =>
                registerIds.Contains(p.CashRegisterId)
                && p.IsActive
                && p.RksvSpecialReceiptYear == year
                && (
                    p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg
                    || p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Jahresbeleg)
                && (query.CashRegisterId == null || p.CashRegisterId == query.CashRegisterId.Value))
            .Select(p => new
            {
                p.Id,
                p.CashRegisterId,
                p.RksvSpecialReceiptKind,
                p.RksvSpecialReceiptYear,
                p.RksvSpecialReceiptMonth,
                p.CreatedAt,
                p.CreatedBy,
                p.CashierId,
                p.TseSignature,
                p.Notes,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var paymentIds = payments.Select(p => p.Id).ToList();
        var fonByPayment = await _db.RksvSpecialReceiptFinanzOnlineSubmissions.AsNoTracking()
            .Where(s => paymentIds.Contains(s.PaymentId))
            .Select(s => new { s.PaymentId, s.Status })
            .ToDictionaryAsync(s => s.PaymentId, s => s.Status, cancellationToken)
            .ConfigureAwait(false);

        var autoRuns = await _db.MonatsbelegAutoRuns.AsNoTracking()
            .Where(r =>
                r.TenantId == tenantId
                && r.Year == year
                && (query.CashRegisterId == null || r.CashRegisterId == query.CashRegisterId.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var autoByPayment = autoRuns
            .Where(r => r.PaymentId.HasValue)
            .GroupBy(r => r.PaymentId!.Value)
            .ToDictionary(g => g.Key, g => g.First());
        var failedRuns = autoRuns
            .Where(r =>
                r.Status == MonatsbelegAutoRunStatuses.Failed
                || r.Status == MonatsbelegAutoRunStatuses.Exhausted)
            .Where(r => !r.PaymentId.HasValue || payments.All(p => p.Id != r.PaymentId.Value))
            .ToList();

        var creatorIds = payments
            .Select(p => string.IsNullOrWhiteSpace(p.CreatedBy) ? p.CashierId : p.CreatedBy)
            .Where(id => !string.IsNullOrWhiteSpace(id) && id != AutoMonatsbelegCutoff.SystemActorUserId)
            .Distinct()
            .ToList();
        var names = creatorIds.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await _db.Users.AsNoTracking()
                .Where(u => creatorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName })
                .ToDictionaryAsync(
                    u => u.Id,
                    u => FormatPerson(u.FirstName, u.LastName, u.UserName),
                    cancellationToken)
                .ConfigureAwait(false);

        var rows = new List<MonatsbelegListRowDto>(payments.Count + failedRuns.Count);
        foreach (var p in payments)
        {
            var month = p.RksvSpecialReceiptMonth
                ?? (string.Equals(p.RksvSpecialReceiptKind, RksvSpecialReceiptKinds.Jahresbeleg, StringComparison.Ordinal)
                    ? 12
                    : 0);
            if (month is < 1 or > 12)
                continue;

            var isJahresbeleg =
                string.Equals(p.RksvSpecialReceiptKind, RksvSpecialReceiptKinds.Jahresbeleg, StringComparison.Ordinal)
                || month == 12;
            var actorId = string.IsNullOrWhiteSpace(p.CreatedBy) ? p.CashierId : p.CreatedBy;
            var auto = autoByPayment.GetValueOrDefault(p.Id);
            var autoCreated = auto != null
                || string.Equals(actorId, AutoMonatsbelegCutoff.SystemActorUserId, StringComparison.OrdinalIgnoreCase)
                || (p.Notes?.Contains("Auto-Monatsbeleg", StringComparison.OrdinalIgnoreCase) ?? false);

            rows.Add(new MonatsbelegListRowDto
            {
                PaymentId = p.Id,
                CashRegisterId = p.CashRegisterId,
                RegisterNumber = registerById.GetValueOrDefault(p.CashRegisterId)?.RegisterNumber ?? "",
                RegisterLocation = registerById.GetValueOrDefault(p.CashRegisterId)?.Location,
                Year = p.RksvSpecialReceiptYear ?? year,
                Month = month,
                Period = $"{p.RksvSpecialReceiptYear ?? year:D4}-{month:D2}",
                CreatedAtUtc = p.CreatedAt,
                CreatedBy = ResolveCreatedBy(actorId, names),
                CreatedByUserId = actorId ?? "",
                TseSignature = AutoMonatsbelegCutoff.MaskTseSignature(p.TseSignature),
                DepStatus = AutoMonatsbelegCutoff.DepStatus(p.TseSignature),
                FonStatus = ResolveFonStatus(isJahresbeleg, fonByPayment.GetValueOrDefault(p.Id)),
                IsJahresbeleg = isJahresbeleg,
                AutoCreated = autoCreated,
                Status = "created",
                AttemptCount = auto?.AttemptCount ?? 0,
                CorrelationId = auto?.CorrelationId,
            });
        }

        foreach (var run in failedRuns)
        {
            rows.Add(new MonatsbelegListRowDto
            {
                PaymentId = run.PaymentId,
                CashRegisterId = run.CashRegisterId,
                RegisterNumber = registerById.GetValueOrDefault(run.CashRegisterId)?.RegisterNumber ?? "",
                RegisterLocation = registerById.GetValueOrDefault(run.CashRegisterId)?.Location,
                Year = run.Year,
                Month = run.Month,
                Period = $"{run.Year:D4}-{run.Month:D2}",
                CreatedAtUtc = run.LastAttemptUtc,
                CreatedBy = AutoMonatsbelegCutoff.SystemActorRole,
                CreatedByUserId = AutoMonatsbelegCutoff.SystemActorUserId,
                TseSignature = string.Empty,
                DepStatus = "Missing",
                FonStatus = run.Month == 12 ? RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Pending : RksvSpecialReceiptFinanzOnlineSubmissionStatuses.NotRequired,
                IsJahresbeleg = run.Month == 12,
                AutoCreated = true,
                Status = "failed",
                LastError = run.LastError,
                AttemptCount = run.AttemptCount,
                CorrelationId = run.CorrelationId,
            });
        }

        IEnumerable<MonatsbelegListRowDto> filtered = rows;
        if (status == "created")
            filtered = rows.Where(r => r.Status == "created");
        else if (status == "failed")
            filtered = rows.Where(r => r.Status == "failed");
        else if (status == "fonPending")
            filtered = rows.Where(r =>
                r.IsJahresbeleg
                && r.Status == "created"
                && !string.Equals(r.FonStatus, RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Verified, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(r.FonStatus, RksvSpecialReceiptFinanzOnlineSubmissionStatuses.NotRequired, StringComparison.OrdinalIgnoreCase));

        var ordered = filtered
            .OrderByDescending(r => r.Month)
            .ThenBy(r => r.RegisterNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var hasFailed = autoRuns.Any(r =>
            r.Status == MonatsbelegAutoRunStatuses.Exhausted || r.Status == MonatsbelegAutoRunStatuses.Failed);

        return new MonatsbelegListResponse
        {
            Year = year,
            Total = total,
            HasFailedAutoCreates = hasFailed,
            Items = items,
        };
    }

    private static string NormalizeStatus(string? status)
    {
        var raw = (status ?? "all").Trim();
        if (raw.Equals("created", StringComparison.OrdinalIgnoreCase))
            return "created";
        if (raw.Equals("failed", StringComparison.OrdinalIgnoreCase))
            return "failed";
        if (raw.Equals("fonPending", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("fon-pending", StringComparison.OrdinalIgnoreCase))
            return "fonPending";
        return "all";
    }

    private static string ResolveFonStatus(bool isJahresbeleg, string? stored)
    {
        if (!isJahresbeleg)
            return RksvSpecialReceiptFinanzOnlineSubmissionStatuses.NotRequired;
        return string.IsNullOrWhiteSpace(stored)
            ? RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Pending
            : stored;
    }

    private static string ResolveCreatedBy(string? actorId, IReadOnlyDictionary<string, string> names)
    {
        if (string.IsNullOrWhiteSpace(actorId)
            || string.Equals(actorId, AutoMonatsbelegCutoff.SystemActorUserId, StringComparison.OrdinalIgnoreCase))
            return AutoMonatsbelegCutoff.SystemActorRole;
        if (names.TryGetValue(actorId, out var name) && !string.IsNullOrWhiteSpace(name))
            return name;
        return actorId;
    }

    private static string FormatPerson(string? first, string? last, string? userName)
    {
        var name = $"{first} {last}".Trim();
        return string.IsNullOrWhiteSpace(name) ? userName?.Trim() ?? "" : name;
    }
}
