using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed record LicenseExportFilters(
    DateTime? FromUtc,
    DateTime? ToUtc,
    bool IncludeActivationHistory,
    bool MaskLicenseKeys);

public sealed class LicenseReportSummaryDto
{
    public DateTime GeneratedAtUtc { get; set; }

    public int IssuedTotalInDateFilter { get; set; }

    public int IssuedActiveEligible { get; set; }

    public int IssuedRevoked { get; set; }

    public int IssuedCancelled { get; set; }

    public int IssuedDeleted { get; set; }

    public int ExpiringWithin30Days { get; set; }

    public int ExpiringWithin15Days { get; set; }

    public int ExpiringWithin7Days { get; set; }

    public int UniqueActivatedDevices { get; set; }

    public int ActivationAttemptsInDateFilter { get; set; }
}

public sealed class LicenseExportIssuedRowDto
{
    public Guid Id { get; set; }

    public string LicenseKey { get; set; } = "";

    public string CustomerName { get; set; } = "";

    public DateTime ExpiryAtUtc { get; set; }

    public bool RequireFingerprint { get; set; }

    public string? MachineHashHex { get; set; }

    public DateTime IssuedAtUtc { get; set; }

    public string? IssuedByUserId { get; set; }

    public bool IsRevoked { get; set; }

    public bool IsCancelled { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public Guid? SupersededByLicenseId { get; set; }

    public Guid? TransferredToLicenseId { get; set; }

    /// <summary>JWT is never exported.</summary>
    public string SignedJwtOmitted { get; set; } = "omitted";
}

public sealed class LicenseExportActivatedRowDto
{
    public Guid Id { get; set; }

    public string LicenseKey { get; set; } = "";

    public string CustomerName { get; set; } = "";

    public DateTime ValidUntilUtc { get; set; }

    public string MachineFingerprint { get; set; } = "";

    public DateTime ActivatedAtUtc { get; set; }

    public DateTime LastSeenAtUtc { get; set; }
}

public sealed class LicenseExportAttemptRowDto
{
    public Guid Id { get; set; }

    public string LicenseKey { get; set; } = "";

    public string MachineFingerprint { get; set; } = "";

    public string ActivationStatus { get; set; } = "";

    public string? FailureReason { get; set; }

    public string? ClientIp { get; set; }

    public string? UserAgent { get; set; }

    public DateTime ActivatedAtUtc { get; set; }

    public DateTime? DeactivatedAtUtc { get; set; }
}

public sealed class LicenseFullExportPayloadDto
{
    public DateTime ExportedAtUtc { get; set; }

    public LicenseExportFiltersDto Filters { get; set; } = new();

    public IReadOnlyList<LicenseExportIssuedRowDto> IssuedLicenses { get; set; } = [];

    public IReadOnlyList<LicenseExportActivatedRowDto> ActivatedLicenses { get; set; } = [];

    public IReadOnlyList<LicenseExportAttemptRowDto>? ActivationAttempts { get; set; }
}

public sealed class LicenseExportFiltersDto
{
    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public bool IncludeActivationHistory { get; set; }

    public bool MaskLicenseKeys { get; set; }
}

public interface ILicenseExportReportService
{
    Task<LicenseReportSummaryDto> GetSummaryAsync(LicenseExportFilters filters, CancellationToken cancellationToken);

    Task<LicenseFullExportPayloadDto> BuildExportPayloadAsync(LicenseExportFilters filters, CancellationToken cancellationToken);

    Task<byte[]> BuildCsvAsync(LicenseExportFilters filters, CancellationToken cancellationToken);

    Task<(byte[] Utf8Bytes, string ContentType)> BuildJsonAsync(LicenseExportFilters filters, CancellationToken cancellationToken);
}

public sealed class LicenseExportReportService : ILicenseExportReportService
{
    private const int MaxActivationAttemptsExport = 50_000;

    private static readonly JsonSerializerOptions JsonExportOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly AppDbContext _db;

    public LicenseExportReportService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<LicenseReportSummaryDto> GetSummaryAsync(LicenseExportFilters filters, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var issuedBase = _db.IssuedLicenses.AsNoTracking();
        issuedBase = ApplyIssuedDateFilter(issuedBase, filters.FromUtc, filters.ToUtc);

        var issuedList = await issuedBase.ToListAsync(cancellationToken).ConfigureAwait(false);

        var activeEligible = issuedList.Count(il =>
            !il.IsDeleted
            && !il.IsCancelled
            && !il.IsRevoked
            && il.SupersededByLicenseId == null
            && il.TransferredToLicenseId == null
            && il.ExpiryAtUtc >= now);

        int CountExpiringWithinDays(int maxDaysInclusive)
        {
            var limit = now.AddDays(maxDaysInclusive);
            return issuedList.Count(il =>
                !il.IsDeleted
                && !il.IsCancelled
                && !il.IsRevoked
                && il.SupersededByLicenseId == null
                && il.TransferredToLicenseId == null
                && il.ExpiryAtUtc >= now
                && il.ExpiryAtUtc <= limit);
        }

        var uniqueDevices = await _db.ActivatedLicenses.AsNoTracking()
            .Select(a => a.MachineFingerprint)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var attemptQ = _db.LicenseActivationAttempts.AsNoTracking();
        attemptQ = ApplyAttemptDateFilter(attemptQ, filters.FromUtc, filters.ToUtc);
        var attemptCount = await attemptQ.CountAsync(cancellationToken).ConfigureAwait(false);

        return new LicenseReportSummaryDto
        {
            GeneratedAtUtc = now,
            IssuedTotalInDateFilter = issuedList.Count,
            IssuedActiveEligible = activeEligible,
            IssuedRevoked = issuedList.Count(i => i.IsRevoked),
            IssuedCancelled = issuedList.Count(i => i.IsCancelled),
            IssuedDeleted = issuedList.Count(i => i.IsDeleted),
            ExpiringWithin30Days = CountExpiringWithinDays(30),
            ExpiringWithin15Days = CountExpiringWithinDays(15),
            ExpiringWithin7Days = CountExpiringWithinDays(7),
            UniqueActivatedDevices = uniqueDevices,
            ActivationAttemptsInDateFilter = attemptCount,
        };
    }

    /// <inheritdoc />
    public async Task<LicenseFullExportPayloadDto> BuildExportPayloadAsync(
        LicenseExportFilters filters,
        CancellationToken cancellationToken)
    {
        var issued = await QueryIssuedForExport(filters, cancellationToken).ConfigureAwait(false);
        var keys = issued.Select(i => i.LicenseKey.Trim().ToUpperInvariant()).Distinct().ToHashSet(StringComparer.OrdinalIgnoreCase);

        var activatedAll = await _db.ActivatedLicenses.AsNoTracking()
            .OrderByDescending(a => a.ActivatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var activated = activatedAll
            .Where(a => keys.Contains(a.LicenseKey.Trim().ToUpperInvariant()))
            .ToList();

        List<LicenseExportAttemptRowDto>? attempts = null;
        if (filters.IncludeActivationHistory)
        {
            var aq = _db.LicenseActivationAttempts.AsNoTracking();
            aq = ApplyAttemptDateFilter(aq, filters.FromUtc, filters.ToUtc);
            var raw = await aq
                .OrderByDescending(a => a.ActivatedAtUtc)
                .Take(MaxActivationAttemptsExport)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            attempts = raw.Select(a => new LicenseExportAttemptRowDto
            {
                Id = a.Id,
                LicenseKey = MaskKeyIfNeeded(a.LicenseKey, filters.MaskLicenseKeys),
                MachineFingerprint = a.MachineFingerprint,
                ActivationStatus = a.ActivationStatus.ToString(),
                FailureReason = a.FailureReason,
                ClientIp = a.ClientIp,
                UserAgent = a.UserAgent,
                ActivatedAtUtc = a.ActivatedAtUtc,
                DeactivatedAtUtc = a.DeactivatedAtUtc,
            }).ToList();
        }

        return new LicenseFullExportPayloadDto
        {
            ExportedAtUtc = DateTime.UtcNow,
            Filters = new LicenseExportFiltersDto
            {
                FromUtc = filters.FromUtc,
                ToUtc = filters.ToUtc,
                IncludeActivationHistory = filters.IncludeActivationHistory,
                MaskLicenseKeys = filters.MaskLicenseKeys,
            },
            IssuedLicenses = issued.Select(MapIssued).Select(r => ApplyMaskIssued(r, filters.MaskLicenseKeys)).ToList(),
            ActivatedLicenses = activated.Select(MapActivated).Select(r => ApplyMaskActivated(r, filters.MaskLicenseKeys)).ToList(),
            ActivationAttempts = attempts,
        };
    }

    /// <inheritdoc />
    public async Task<byte[]> BuildCsvAsync(LicenseExportFilters filters, CancellationToken cancellationToken)
    {
        var payload = await BuildExportPayloadAsync(filters, cancellationToken).ConfigureAwait(false);
        var sb = new StringBuilder(8192);
        var inv = CultureInfo.InvariantCulture;

        sb.AppendLine("# issued_licenses");
        sb.AppendLine(
            "id,customer_name,license_key,expiry_at_utc,issued_at_utc,require_fingerprint,machine_hash_hex," +
            "is_revoked,is_cancelled,is_deleted,revoked_at_utc,cancelled_at_utc,superseded_by_license_id,transferred_to_license_id");

        foreach (var i in payload.IssuedLicenses)
        {
            sb.Append(EscapeCsv(i.Id.ToString())).Append(',')
                .Append(EscapeCsv(i.CustomerName)).Append(',')
                .Append(EscapeCsv(i.LicenseKey)).Append(',')
                .Append(EscapeCsv(i.ExpiryAtUtc.ToString("o", inv))).Append(',')
                .Append(EscapeCsv(i.IssuedAtUtc.ToString("o", inv))).Append(',')
                .Append(i.RequireFingerprint ? '1' : '0').Append(',')
                .Append(EscapeCsv(i.MachineHashHex)).Append(',')
                .Append(i.IsRevoked ? '1' : '0').Append(',')
                .Append(i.IsCancelled ? '1' : '0').Append(',')
                .Append(i.IsDeleted ? '1' : '0').Append(',')
                .Append(EscapeCsv(i.RevokedAtUtc?.ToString("o", inv))).Append(',')
                .Append(EscapeCsv(i.CancelledAtUtc?.ToString("o", inv))).Append(',')
                .Append(EscapeCsv(i.SupersededByLicenseId?.ToString())).Append(',')
                .Append(EscapeCsv(i.TransferredToLicenseId?.ToString()))
                .AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("# activated_licenses");
        sb.AppendLine("id,license_key,customer_name,valid_until_utc,machine_fingerprint,activated_at_utc,last_seen_at_utc");

        foreach (var a in payload.ActivatedLicenses)
        {
            sb.Append(EscapeCsv(a.Id.ToString())).Append(',')
                .Append(EscapeCsv(a.LicenseKey)).Append(',')
                .Append(EscapeCsv(a.CustomerName)).Append(',')
                .Append(EscapeCsv(a.ValidUntilUtc.ToString("o", inv))).Append(',')
                .Append(EscapeCsv(a.MachineFingerprint)).Append(',')
                .Append(EscapeCsv(a.ActivatedAtUtc.ToString("o", inv))).Append(',')
                .Append(EscapeCsv(a.LastSeenAtUtc.ToString("o", inv)))
                .AppendLine();
        }

        if (payload.ActivationAttempts is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("# activation_attempts");
            sb.AppendLine(
                "id,license_key,machine_fingerprint,activation_status,failure_reason,client_ip,user_agent,activated_at_utc,deactivated_at_utc");

            foreach (var t in payload.ActivationAttempts)
            {
                sb.Append(EscapeCsv(t.Id.ToString())).Append(',')
                    .Append(EscapeCsv(t.LicenseKey)).Append(',')
                    .Append(EscapeCsv(t.MachineFingerprint)).Append(',')
                    .Append(EscapeCsv(t.ActivationStatus)).Append(',')
                    .Append(EscapeCsv(t.FailureReason)).Append(',')
                    .Append(EscapeCsv(t.ClientIp)).Append(',')
                    .Append(EscapeCsv(t.UserAgent)).Append(',')
                    .Append(EscapeCsv(t.ActivatedAtUtc.ToString("o", inv))).Append(',')
                    .Append(EscapeCsv(t.DeactivatedAtUtc?.ToString("o", inv)))
                    .AppendLine();
            }
        }

        var utf8 = Encoding.UTF8.GetBytes(sb.ToString());
        return Encoding.UTF8.GetPreamble().Concat(utf8).ToArray();
    }

    /// <inheritdoc />
    public async Task<(byte[] Utf8Bytes, string ContentType)> BuildJsonAsync(
        LicenseExportFilters filters,
        CancellationToken cancellationToken)
    {
        var payload = await BuildExportPayloadAsync(filters, cancellationToken).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(payload, JsonExportOptions);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(json)).ToArray();
        return (bytes, "application/json; charset=utf-8");
    }

    private async Task<List<IssuedLicense>> QueryIssuedForExport(LicenseExportFilters filters, CancellationToken cancellationToken)
    {
        var q = _db.IssuedLicenses.AsNoTracking();
        q = ApplyIssuedDateFilter(q, filters.FromUtc, filters.ToUtc);
        return await q.OrderByDescending(i => i.IssuedAtUtc).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IQueryable<IssuedLicense> ApplyIssuedDateFilter(
        IQueryable<IssuedLicense> q,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        if (fromUtc.HasValue)
        {
            var f = DateTime.SpecifyKind(fromUtc.Value, DateTimeKind.Utc);
            q = q.Where(il => il.IssuedAtUtc >= f);
        }

        if (toUtc.HasValue)
        {
            var t = DateTime.SpecifyKind(toUtc.Value, DateTimeKind.Utc);
            q = q.Where(il => il.IssuedAtUtc <= t);
        }

        return q;
    }

    private static IQueryable<LicenseActivationAttempt> ApplyAttemptDateFilter(
        IQueryable<LicenseActivationAttempt> q,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        if (fromUtc.HasValue)
        {
            var f = DateTime.SpecifyKind(fromUtc.Value, DateTimeKind.Utc);
            q = q.Where(a => a.ActivatedAtUtc >= f);
        }

        if (toUtc.HasValue)
        {
            var t = DateTime.SpecifyKind(toUtc.Value, DateTimeKind.Utc);
            q = q.Where(a => a.ActivatedAtUtc <= t);
        }

        return q;
    }

    private static LicenseExportIssuedRowDto MapIssued(IssuedLicense il) =>
        new()
        {
            Id = il.Id,
            LicenseKey = il.LicenseKey.Trim().ToUpperInvariant(),
            CustomerName = il.CustomerName,
            ExpiryAtUtc = il.ExpiryAtUtc,
            RequireFingerprint = il.RequireFingerprint,
            MachineHashHex = il.MachineHashHex,
            IssuedAtUtc = il.IssuedAtUtc,
            IssuedByUserId = il.IssuedByUserId,
            IsRevoked = il.IsRevoked,
            IsCancelled = il.IsCancelled,
            IsDeleted = il.IsDeleted,
            RevokedAtUtc = il.RevokedAtUtc,
            CancelledAtUtc = il.CancelledAtUtc,
            SupersededByLicenseId = il.SupersededByLicenseId,
            TransferredToLicenseId = il.TransferredToLicenseId,
        };

    private static LicenseExportActivatedRowDto MapActivated(ActivatedLicense a) =>
        new()
        {
            Id = a.Id,
            LicenseKey = a.LicenseKey.Trim().ToUpperInvariant(),
            CustomerName = a.CustomerName,
            ValidUntilUtc = a.ValidUntilUtc,
            MachineFingerprint = a.MachineFingerprint,
            ActivatedAtUtc = a.ActivatedAtUtc,
            LastSeenAtUtc = a.LastSeenAtUtc,
        };

    private static LicenseExportIssuedRowDto ApplyMaskIssued(LicenseExportIssuedRowDto r, bool mask)
    {
        if (!mask)
            return r;
        r.LicenseKey = MaskIssuedLicenseKey(r.LicenseKey);
        return r;
    }

    private static LicenseExportActivatedRowDto ApplyMaskActivated(LicenseExportActivatedRowDto r, bool mask)
    {
        if (!mask)
            return r;
        r.LicenseKey = MaskIssuedLicenseKey(r.LicenseKey);
        return r;
    }

    private static string MaskKeyIfNeeded(string key, bool mask) =>
        mask ? MaskIssuedLicenseKey(key) : key.Trim().ToUpperInvariant();

    /// <summary>REGK-****-****- plus last segment; non-standard shapes fully redacted.</summary>
    public static string MaskIssuedLicenseKey(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return "REGK-****-****-*****";

        var parts = licenseKey.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 4
            && string.Equals(parts[0], "REGK", StringComparison.OrdinalIgnoreCase)
            && parts[3].Length > 0)
        {
            return "REGK-****-****-" + parts[3].ToUpperInvariant();
        }

        return "REGK-****-****-*****";
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";
        var s = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{s}\"";
    }
}
