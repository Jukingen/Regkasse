using System.Globalization;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IDepExportValidationService
{
    /// <summary>
    /// Validates a DEP export history row. When <paramref name="exportJson"/> is null,
    /// loads JSON from <see cref="DepExportHistory.StoragePath"/> if present.
    /// </summary>
    Task<DepExportHistoryValidationResult> ValidateExportAsync(
        Guid exportId,
        string? exportJson = null,
        CancellationToken cancellationToken = default);

    Task<DepExportValidationReport> GetValidationReportAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> IsExportValidAsync(Guid exportId, CancellationToken cancellationToken = default);

    /// <summary>Returns the persisted validation report for a history row, or a status-only stub.</summary>
    Task<DepExportHistoryValidationResult?> GetStoredValidationAsync(
        Guid exportId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Automatic post-export validation for BMF DEP §7 JSON stored in <see cref="DepExportHistory"/>.
/// Reuses structural rules from <see cref="IRksvDepExportService.ValidateExportFormatAsync"/>.
/// </summary>
public sealed class DepExportValidationService : IDepExportValidationService
{
    private static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly AppDbContext _db;
    private readonly IRksvDepExportService _depExportService;
    private readonly IActivityEventService _activity;
    private readonly ILogger<DepExportValidationService> _logger;

    public DepExportValidationService(
        AppDbContext db,
        IRksvDepExportService depExportService,
        IActivityEventService activity,
        ILogger<DepExportValidationService> logger)
    {
        _db = db;
        _depExportService = depExportService;
        _activity = activity;
        _logger = logger;
    }

    public async Task<DepExportHistoryValidationResult> ValidateExportAsync(
        Guid exportId,
        string? exportJson = null,
        CancellationToken cancellationToken = default)
    {
        var export = await _db.DepExportHistories
            .FirstOrDefaultAsync(h => h.Id == exportId, cancellationToken)
            .ConfigureAwait(false);

        if (export is null)
            return DepExportHistoryValidationResult.Fail(exportId, "Export not found");

        if (!string.Equals(export.Status, DepExportStatus.Completed.ToString(), StringComparison.Ordinal))
        {
            return await PersistResultAsync(
                    export,
                    DepExportHistoryValidationResult.Fail(exportId, "Only completed exports can be validated."),
                    notifyFailure: false,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            var json = exportJson;
            if (string.IsNullOrWhiteSpace(json))
                json = await TryLoadStoredJsonAsync(export, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(json))
            {
                var missing = new DepExportHistoryValidationResult
                {
                    ExportId = exportId,
                    TenantId = export.TenantId,
                    IsValid = false,
                    ValidatedAt = DateTime.UtcNow,
                    ErrorMessage = "Export JSON not available (no in-memory payload and no stored file).",
                    Checks =
                    [
                        new DepExportValidationCheck
                        {
                            Name = "JSON Structure",
                            Passed = false,
                            Details = "Stored export file not available for validation.",
                        },
                    ],
                };
                return await PersistResultAsync(export, missing, notifyFailure: true, cancellationToken)
                    .ConfigureAwait(false);
            }

            var format = await _depExportService
                .ValidateExportFormatAsync(json, cancellationToken)
                .ConfigureAwait(false);

            var jsonCheck = BuildJsonStructureCheck(format);
            var signatureCheck = BuildSignatureChainCheck(json, format, export.SignatureCount);
            var certificateCheck = BuildCertificateCheck(json, format);
            var taxCheck = BuildTaxRateCheck(json);

            var result = new DepExportHistoryValidationResult
            {
                ExportId = exportId,
                TenantId = export.TenantId,
                IsValid = jsonCheck.Passed && signatureCheck.Passed && certificateCheck.Passed && taxCheck.Passed,
                ValidatedAt = DateTime.UtcNow,
                Checks = [jsonCheck, signatureCheck, certificateCheck, taxCheck],
            };

            return await PersistResultAsync(export, result, notifyFailure: !result.IsValid, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEP export validation failed for history {ExportId}", exportId);
            var failed = DepExportHistoryValidationResult.Fail(exportId, $"Validation error: {ex.Message}");
            failed.TenantId = export.TenantId;
            return await PersistResultAsync(export, failed, notifyFailure: true, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task<DepExportValidationReport> GetValidationReportAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.DepExportHistories
            .AsNoTracking()
            .Where(h => h.TenantId == tenantId && h.Status == DepExportStatus.Completed.ToString())
            .OrderByDescending(h => h.ExportedAt)
            .Take(100)
            .Select(h => new
            {
                h.Id,
                h.CashRegisterId,
                h.FileName,
                h.ExportedAt,
                h.ValidationStatus,
                h.ValidatedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var passed = rows.Count(r => r.ValidationStatus == DepExportValidationStatuses.Passed);
        var failed = rows.Count(r => r.ValidationStatus == DepExportValidationStatuses.Failed);
        var skipped = rows.Count(r => r.ValidationStatus == DepExportValidationStatuses.Skipped);
        var pending = rows.Count(r =>
            string.IsNullOrWhiteSpace(r.ValidationStatus) ||
            r.ValidationStatus == DepExportValidationStatuses.Pending);

        return new DepExportValidationReport
        {
            TenantId = tenantId,
            GeneratedAtUtc = DateTime.UtcNow,
            TotalExports = rows.Count,
            PassedCount = passed,
            FailedCount = failed,
            PendingCount = pending,
            SkippedCount = skipped,
            AllValidatedPassed = rows.Count > 0 && failed == 0 && pending == 0,
            Recent = rows.Select(r => new DepExportHistoryValidationSummaryItem
            {
                ExportId = r.Id,
                CashRegisterId = r.CashRegisterId,
                FileName = r.FileName,
                ExportedAt = r.ExportedAt,
                ValidationStatus = r.ValidationStatus,
                ValidatedAt = r.ValidatedAt,
                IsValid = r.ValidationStatus switch
                {
                    DepExportValidationStatuses.Passed => true,
                    DepExportValidationStatuses.Failed => false,
                    _ => null,
                },
            }).ToList(),
        };
    }

    public async Task<bool> IsExportValidAsync(Guid exportId, CancellationToken cancellationToken = default)
    {
        var status = await _db.DepExportHistories
            .AsNoTracking()
            .Where(h => h.Id == exportId)
            .Select(h => h.ValidationStatus)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return status == DepExportValidationStatuses.Passed;
    }

    public async Task<DepExportHistoryValidationResult?> GetStoredValidationAsync(
        Guid exportId,
        CancellationToken cancellationToken = default)
    {
        var export = await _db.DepExportHistories
            .AsNoTracking()
            .Where(h => h.Id == exportId)
            .Select(h => new
            {
                h.Id,
                h.TenantId,
                h.ValidationStatus,
                h.ValidatedAt,
                h.ValidationReportJson,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (export is null)
            return null;

        if (!string.IsNullOrWhiteSpace(export.ValidationReportJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<DepExportHistoryValidationResult>(
                    export.ValidationReportJson,
                    ReportJsonOptions);
                if (parsed is not null)
                {
                    parsed.ExportId = export.Id;
                    parsed.TenantId = export.TenantId;
                    return parsed;
                }
            }
            catch (JsonException)
            {
                // Fall through to status-only summary.
            }
        }

        return new DepExportHistoryValidationResult
        {
            ExportId = export.Id,
            TenantId = export.TenantId,
            IsValid = export.ValidationStatus == DepExportValidationStatuses.Passed,
            ValidatedAt = export.ValidatedAt ?? default,
            ErrorMessage = string.IsNullOrWhiteSpace(export.ValidationStatus) ||
                           export.ValidationStatus == DepExportValidationStatuses.Pending
                ? "Validation has not been run yet."
                : null,
            Checks = Array.Empty<DepExportValidationCheck>(),
        };
    }

    private async Task<DepExportHistoryValidationResult> PersistResultAsync(
        DepExportHistory export,
        DepExportHistoryValidationResult result,
        bool notifyFailure,
        CancellationToken cancellationToken)
    {
        result.ExportId = export.Id;
        result.TenantId = export.TenantId;
        result.ValidatedAt = DateTime.UtcNow;

        export.ValidatedAt = result.ValidatedAt;
        export.ValidationStatus = result.IsValid
            ? DepExportValidationStatuses.Passed
            : DepExportValidationStatuses.Failed;
        export.ValidationReportJson = JsonSerializer.Serialize(result, ReportJsonOptions);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (notifyFailure && !result.IsValid)
            await NotifyValidationFailureAsync(export, result, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private async Task NotifyValidationFailureAsync(
        DepExportHistory export,
        DepExportHistoryValidationResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var failedChecks = result.Checks.Where(c => !c.Passed).Select(c => c.Name).ToList();
            var details = failedChecks.Count > 0
                ? string.Join(", ", failedChecks)
                : result.ErrorMessage ?? "Unknown validation failure";

            await _activity.PublishAsync(
                    new ActivityEventPublishRequest(
                        export.TenantId,
                        ActivityEventType.DepExportValidationFailed,
                        Title: "DEP Export Validierung fehlgeschlagen",
                        Description:
                            $"Automatische Validierung für {export.FileName} fehlgeschlagen: {details}",
                        Severity: ActivitySeverityNames.Error,
                        DedupKey: $"dep-export-validation-failed:{export.Id:D}",
                        EntityType: "dep_export_history",
                        EntityId: export.Id.ToString("D"),
                        Metadata: new Dictionary<string, object>
                        {
                            ["exportId"] = export.Id.ToString("D"),
                            ["fileName"] = export.FileName,
                            ["cashRegisterId"] = export.CashRegisterId.ToString("D"),
                            ["failedChecks"] = details,
                            ["deepLink"] = "/admin/rksv/dep-export",
                        }),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to publish DEP validation failure activity for {ExportId}",
                export.Id);
        }
    }

    private static async Task<string?> TryLoadStoredJsonAsync(
        DepExportHistory export,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(export.StoragePath) || !File.Exists(export.StoragePath))
            return null;

        return await File.ReadAllTextAsync(export.StoragePath, cancellationToken).ConfigureAwait(false);
    }

    internal static DepExportValidationCheck BuildJsonStructureCheck(RksvDepExportValidationResult format)
    {
        var structuralErrors = format.Errors
            .Where(e =>
                e.Contains("Invalid JSON", StringComparison.OrdinalIgnoreCase) ||
                e.Contains("Belege-Gruppe", StringComparison.OrdinalIgnoreCase) ||
                e.Contains("must be a JSON array", StringComparison.OrdinalIgnoreCase) ||
                e.Contains("at least one certificate group", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var passed = structuralErrors.Count == 0 && format.BelegeGruppeCount > 0;
        return new DepExportValidationCheck
        {
            Name = "JSON Structure",
            Passed = passed,
            Details = passed
                ? $"BMF structure OK ({format.BelegeGruppeCount} groups, {format.BelegCount} receipts)."
                : structuralErrors.Count > 0
                    ? string.Join(" ", structuralErrors.Take(5))
                    : format.Errors.Count > 0
                        ? string.Join(" ", format.Errors.Take(5))
                        : "Missing Belege-Gruppe.",
        };
    }

    internal static DepExportValidationCheck BuildSignatureChainCheck(
        string exportJson,
        RksvDepExportValidationResult format,
        int expectedSignatureCount)
    {
        var jwsErrors = format.Errors
            .Where(e => e.Contains("compact JWS", StringComparison.OrdinalIgnoreCase) ||
                        e.Contains("Belege-kompakt", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var belegCount = CountBelegeKompakt(exportJson);
        var countMismatch = expectedSignatureCount > 0 && belegCount != expectedSignatureCount;

        if (jwsErrors.Count > 0 || belegCount <= 0)
        {
            return new DepExportValidationCheck
            {
                Name = "Signature Chain",
                Passed = false,
                Details = jwsErrors.Count > 0
                    ? string.Join(" ", jwsErrors.Take(5))
                    : "No compact JWS receipts found in Belege-kompakt.",
            };
        }

        if (countMismatch)
        {
            return new DepExportValidationCheck
            {
                Name = "Signature Chain",
                Passed = false,
                Details =
                    $"Signature count mismatch: history={expectedSignatureCount}, JSON={belegCount}.",
            };
        }

        return new DepExportValidationCheck
        {
            Name = "Signature Chain",
            Passed = true,
            Details = $"{belegCount} compact JWS receipt(s) present and well-formed.",
        };
    }

    internal static DepExportValidationCheck BuildCertificateCheck(
        string exportJson,
        RksvDepExportValidationResult format)
    {
        var certErrors = format.Errors
            .Where(e =>
                e.Contains("Signaturzertifikat", StringComparison.OrdinalIgnoreCase) ||
                e.Contains("Zertifizierungsstellen", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (certErrors.Count > 0)
        {
            return new DepExportValidationCheck
            {
                Name = "Certificates",
                Passed = false,
                Details = string.Join(" ", certErrors.Take(5)),
            };
        }

        var groups = CountCertificateGroups(exportJson);
        var warningNote = format.Warnings.Count > 0
            ? " Warnings: " + string.Join(" ", format.Warnings.Take(3))
            : string.Empty;

        return new DepExportValidationCheck
        {
            Name = "Certificates",
            Passed = groups > 0,
            Details = groups > 0
                ? $"{groups} certificate group(s) with Signaturzertifikat present.{warningNote}"
                : "No certificate groups found.",
        };
    }

    internal static DepExportValidationCheck BuildTaxRateCheck(string exportJson)
    {
        var decoded = 0;
        var negatives = 0;
        var parseFailures = 0;

        try
        {
            using var doc = JsonDocument.Parse(exportJson);
            if (!doc.RootElement.TryGetProperty("Belege-Gruppe", out var groups) ||
                groups.ValueKind != JsonValueKind.Array)
            {
                return new DepExportValidationCheck
                {
                    Name = "Tax Rates",
                    Passed = false,
                    Details = "Cannot validate tax amounts: missing Belege-Gruppe.",
                };
            }

            foreach (var group in groups.EnumerateArray())
            {
                if (!group.TryGetProperty("Belege-kompakt", out var belege) ||
                    belege.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var beleg in belege.EnumerateArray())
                {
                    if (beleg.ValueKind != JsonValueKind.String)
                        continue;

                    var payloadText = TryDecodeJwsPayload(beleg.GetString());
                    if (payloadText is null)
                    {
                        parseFailures++;
                        continue;
                    }

                    if (TryReadTaxAmounts(payloadText, out var amounts))
                    {
                        decoded++;
                        if (amounts.Any(a => a < 0m))
                            negatives++;
                    }
                    else
                    {
                        parseFailures++;
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            return new DepExportValidationCheck
            {
                Name = "Tax Rates",
                Passed = false,
                Details = $"Tax validation failed: {ex.Message}",
            };
        }

        if (negatives > 0)
        {
            return new DepExportValidationCheck
            {
                Name = "Tax Rates",
                Passed = false,
                Details = $"{negatives} receipt payload(s) contain negative tax-set amounts.",
            };
        }

        if (decoded > 0)
        {
            return new DepExportValidationCheck
            {
                Name = "Tax Rates",
                Passed = true,
                Details = $"{decoded} receipt payload(s) have non-negative RKSV tax-set amounts.",
            };
        }

        return new DepExportValidationCheck
        {
            Name = "Tax Rates",
            Passed = true,
            Details =
                parseFailures > 0
                    ? "No decodeable tax-set payloads in compact JWS (accepted for BMF DEP root JSON)."
                    : "No receipt payloads to inspect; BMF DEP root does not carry tax-rate tables.",
        };
    }

    private static int CountBelegeKompakt(string exportJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(exportJson);
            if (!doc.RootElement.TryGetProperty("Belege-Gruppe", out var groups) ||
                groups.ValueKind != JsonValueKind.Array)
                return 0;

            var count = 0;
            foreach (var group in groups.EnumerateArray())
            {
                if (!group.TryGetProperty("Belege-kompakt", out var belege) ||
                    belege.ValueKind != JsonValueKind.Array)
                    continue;
                count += belege.GetArrayLength();
            }

            return count;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static int CountCertificateGroups(string exportJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(exportJson);
            if (!doc.RootElement.TryGetProperty("Belege-Gruppe", out var groups) ||
                groups.ValueKind != JsonValueKind.Array)
                return 0;
            return groups.GetArrayLength();
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    internal static string? TryDecodeJwsPayload(string? compactJws)
    {
        if (string.IsNullOrWhiteSpace(compactJws))
            return null;

        var parts = compactJws.Split('.', 3, StringSplitOptions.None);
        if (parts.Length != 3)
            return null;

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            return Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    internal static bool TryReadTaxAmounts(string payloadText, out List<decimal> amounts)
    {
        amounts = [];

        if (payloadText.TrimStart().StartsWith('{'))
        {
            try
            {
                var payload = JsonSerializer.Deserialize<BelegdatenPayload>(payloadText);
                if (payload is null)
                    return false;

                amounts =
                [
                    payload.BetragSatzNormal,
                    payload.BetragSatzErmaessigt1,
                    payload.BetragSatzErmaessigt2,
                    payload.BetragSatzNull,
                    payload.BetragSatzBesonders,
                ];
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        var segments = payloadText.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parsed = new List<decimal>();
        foreach (var segment in segments)
        {
            var hasDecimalSeparator = segment.Contains(',') || segment.Contains('.');
            if (!hasDecimalSeparator)
                continue;

            if (decimal.TryParse(
                    segment.Replace(',', '.'),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                parsed.Add(value);
            }
        }

        if (parsed.Count < 3)
            return false;

        amounts = parsed;
        return true;
    }
}
