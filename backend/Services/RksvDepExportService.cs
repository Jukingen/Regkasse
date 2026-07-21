using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public class RksvDepExportService : IRksvDepExportService
{
    private static readonly TimeSpan MaxPeriod = TimeSpan.FromDays(366);

    internal const string DemoLegalNoticeDe =
        "DEMO / NICHT FISKAL — Dieser DEP-Export (Signaturjournal) dient nur zu Testzwecken. "
        + "Er ersetzt keine amtliche Betriebsprüfung und ist ohne produktive TSE-Zertifikate nicht rechtlich bindend.";

    internal const string ProductionLegalNoticeDe =
        "Dieser DEP-Export (Signaturjournal) dient der Datenerfassung gemäß RKSV §7. "
        + "Er ersetzt keine amtliche Betriebsprüfung; die fiskalische Gültigkeit erfordert eine erfolgreiche BMF-Prüfung.";

    private readonly AppDbContext _context;
    private readonly ITseKeyProvider _tseKeyProvider;
    private readonly IRksvEnvironmentService _rksvEnv;
    private readonly IRksvDepPrueftoolRunner _prueftoolRunner;
    private readonly ILogger<RksvDepExportService> _logger;

    public RksvDepExportService(
        AppDbContext context,
        ITseKeyProvider tseKeyProvider,
        IRksvEnvironmentService rksvEnv,
        IRksvDepPrueftoolRunner prueftoolRunner,
        ILogger<RksvDepExportService> logger)
    {
        _context = context;
        _tseKeyProvider = tseKeyProvider;
        _rksvEnv = rksvEnv;
        _prueftoolRunner = prueftoolRunner;
        _logger = logger;
    }

    public async Task<RksvDepExportRootDto> GenerateDepExportAsync(
        Guid cashRegisterId,
        DateTime fromUtc,
        DateTime toUtc,
        bool includeSpecialReceipts = true,
        bool includeDailyClosings = true,
        CancellationToken cancellationToken = default)
    {
        var from = NormalizeUtc(fromUtc);
        var to = NormalizeUtc(toUtc);
        if (to < from)
            throw new ArgumentException("toUtc must be >= fromUtc.", nameof(toUtc));
        if (to - from > MaxPeriod)
            throw new ArgumentException($"Period must not exceed {MaxPeriod.TotalDays} days.", nameof(toUtc));

        var registerExists = await _context.CashRegisters
            .AsNoTracking()
            .AnyAsync(c => c.Id == cashRegisterId, cancellationToken)
            .ConfigureAwait(false);
        if (!registerExists)
            throw new InvalidOperationException($"Cash register {cashRegisterId} not found.");

        var receipts = await GetReceiptsWithSignaturesAsync(
                cashRegisterId,
                from,
                to,
                includeSpecialReceipts,
                includeDailyClosings,
                cancellationToken)
            .ConfigureAwait(false);

        var groupedByCert = receipts
            .GroupBy(r => NormalizeThumbprint(r.CertificateThumbprint))
            .OrderBy(g => g.Min(r => r.IssuedAt))
            .ToList();

        var belegeGruppe = new List<RksvDepBelegeGruppeDto>();

        foreach (var group in groupedByCert)
        {
            var certificateBase64 = await GetCertificateBase64Async(group.Key, cancellationToken)
                .ConfigureAwait(false);
            var caChain = await GetCertificateChainBase64Async(group.Key, cancellationToken)
                .ConfigureAwait(false);

            var belegeKompakt = OrderReceiptsForDepExport(group)
                .Select(r => r.TseSignature)
                .Where(IsValidCompactJws)
                .ToList();

            if (belegeKompakt.Count == 0)
                continue;

            belegeGruppe.Add(new RksvDepBelegeGruppeDto
            {
                Signaturzertifikat = certificateBase64,
                Zertifizierungsstellen = caChain,
                BelegeKompakt = belegeKompakt,
            });
        }

        _logger.LogInformation(
            "RKSV DEP export: register={RegisterId} period {From}–{To} groups={Groups} belege={Belege} normal={Normal} special={Special} closings={Closings}",
            cashRegisterId,
            from,
            to,
            belegeGruppe.Count,
            belegeGruppe.Sum(g => g.BelegeKompakt.Count),
            receipts.Count(r => r.ReceiptType == RksvDepReceiptTypes.Normal),
            receipts.Count(r => r.ReceiptType is not null
                && r.ReceiptType != RksvDepReceiptTypes.Normal
                && r.ReceiptType != RksvDepReceiptTypes.DailyClosing),
            receipts.Count(r => r.ReceiptType == RksvDepReceiptTypes.DailyClosing));

        return new RksvDepExportRootDto
        {
            BelegeGruppe = belegeGruppe,
        };
    }

    public async Task<RksvDepExportBuildResult> GenerateDepExportWithValidationAsync(
        Guid cashRegisterId,
        DateTime fromUtc,
        DateTime toUtc,
        bool includeSpecialReceipts = true,
        bool includeDailyClosings = true,
        CancellationToken cancellationToken = default)
    {
        var export = await GenerateDepExportAsync(
                cashRegisterId,
                fromUtc,
                toUtc,
                includeSpecialReceipts,
                includeDailyClosings,
                cancellationToken)
            .ConfigureAwait(false);

        var (groupCount, belegCount) = RksvDepExportStats.Count(export);
        var exportJson = JsonSerializer.Serialize(export, BmfJsonOptions);
        var validation = await ValidateExportFormatAsync(exportJson, cancellationToken).ConfigureAwait(false);
        var isDemo = _rksvEnv.IsDemoMode() || _rksvEnv.IsTseSimulated();
        var environment = isDemo ? "Demo" : "Production";

        var registerNumber = await _context.CashRegisters
            .AsNoTracking()
            .Where(c => c.Id == cashRegisterId)
            .Select(c => c.RegisterNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? string.Empty;

        return new RksvDepExportBuildResult
        {
            Root = export,
            CashRegisterId = cashRegisterId,
            RegisterNumber = registerNumber,
            FromUtc = NormalizeUtc(fromUtc),
            ToUtc = NormalizeUtc(toUtc),
            BelegCount = belegCount,
            BelegeGruppeCount = groupCount,
            IsDemo = isDemo,
            Environment = environment,
            FormatValidated = validation.IsValid,
            LegalNotice = isDemo ? DemoLegalNoticeDe : ProductionLegalNoticeDe,
        };
    }

    public Task<RksvDepExportValidationResult> ValidateExportFormatAsync(
        string exportJson,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isDemo = _rksvEnv.IsDemoMode() || _rksvEnv.IsTseSimulated();
        var environment = isDemo ? "Demo" : "Production";
        var errors = new List<string>();
        var warnings = new List<string>();
        var groupCount = 0;
        var belegCount = 0;

        try
        {
            using var doc = JsonDocument.Parse(exportJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Belege-Gruppe", out var belegeGruppe))
            {
                errors.Add("Missing required property 'Belege-Gruppe'.");
            }
            else if (belegeGruppe.ValueKind != JsonValueKind.Array)
            {
                errors.Add("'Belege-Gruppe' must be a JSON array.");
            }
            else
            {
                groupCount = belegeGruppe.GetArrayLength();
                if (groupCount == 0)
                    errors.Add("'Belege-Gruppe' must contain at least one certificate group.");

                var groupIndex = 0;
                foreach (var group in belegeGruppe.EnumerateArray())
                {
                    groupIndex++;
                    ValidateCertificateGroup(group, groupIndex, isDemo, errors, warnings, ref belegCount);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "DEP export format validation failed: invalid JSON");
            errors.Add($"Invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEP export format validation failed");
            errors.Add($"Validation error: {ex.Message}");
        }

        return Task.FromResult(new RksvDepExportValidationResult
        {
            IsValid = errors.Count == 0,
            IsDemo = isDemo,
            Environment = environment,
            BelegeGruppeCount = groupCount,
            BelegCount = belegCount,
            Errors = errors,
            Warnings = warnings,
        });
    }

    public Task<RksvDepPrueftoolResult> RunPrueftoolAsync(
        string exportJson,
        CancellationToken cancellationToken = default) =>
        RunPrueftoolAsync(exportJson, Guid.Empty, forceRun: false, cancellationToken);

    public async Task<RksvDepPrueftoolResult> RunPrueftoolAsync(
        string exportJson,
        Guid cashRegisterId,
        bool forceRun,
        CancellationToken cancellationToken = default)
    {
        RksvDepExportRootDto export;
        try
        {
            export = JsonSerializer.Deserialize<RksvDepExportRootDto>(exportJson, BmfJsonOptions)
                     ?? new RksvDepExportRootDto();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "DEP Prüftool: invalid export JSON");
            return new RksvDepPrueftoolResult
            {
                Success = false,
                IsDemo = _rksvEnv.IsDemoMode() || _rksvEnv.IsTseSimulated(),
                Environment = _rksvEnv.IsDemoMode() ? "Demo" : "Production",
                Message = "Invalid export JSON",
                ToolOutput = ex.Message,
                ValidatedAtUtc = DateTime.UtcNow,
            };
        }

        var isDemo = _rksvEnv.IsDemoMode() || _rksvEnv.IsTseSimulated();
        if (!isDemo && cashRegisterId == Guid.Empty)
        {
            return new RksvDepPrueftoolResult
            {
                Success = false,
                IsDemo = false,
                Environment = "Production",
                Message = "cashRegisterId is required for production Prüftool runs",
                ToolOutput = "Provide cashRegisterId in the request body for crypto material generation.",
                ValidatedAtUtc = DateTime.UtcNow,
            };
        }

        return await RunPrueftoolAsync(cashRegisterId, export, forceRun, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<RksvDepPrueftoolResult> RunPrueftoolAsync(
        Guid cashRegisterId,
        RksvDepExportRootDto export,
        bool forceRun = false,
        CancellationToken cancellationToken = default)
    {
        var isDemo = _rksvEnv.IsDemoMode() || _rksvEnv.IsTseSimulated();
        var environment = isDemo ? "Demo" : "Production";
        var validatedAt = DateTime.UtcNow;

        if (isDemo && !forceRun)
        {
            _logger.LogInformation(
                "DEP Prüftool skipped in demo mode for register {RegisterId}",
                cashRegisterId);

            return new RksvDepPrueftoolResult
            {
                Success = true,
                IsDemo = true,
                Skipped = true,
                Environment = environment,
                Message = "Prüftool validation skipped in demo mode",
                ToolOutput = "Skipped — use forceRun=true or production environment for BMF verification.",
                ValidatedAtUtc = validatedAt,
            };
        }

        if (!_prueftoolRunner.IsAvailable(out var unavailableReason))
        {
            var message = isDemo
                ? "Prüftool not available in demo environment"
                : "Prüftool validation required but BMF verifier is not available";

            _logger.LogWarning(
                "DEP Prüftool unavailable for register {RegisterId}: {Reason}",
                cashRegisterId,
                unavailableReason);

            return new RksvDepPrueftoolResult
            {
                Success = !isDemo ? false : true,
                IsDemo = isDemo,
                Skipped = false,
                Environment = environment,
                Message = message,
                ToolOutput = unavailableReason ?? "Prüftool unavailable",
                ValidatedAtUtc = validatedAt,
            };
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"regkasse-dep-prueftool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var depPath = Path.Combine(tempDir, "dep-export.json");
            var cryptoPath = Path.Combine(tempDir, "crypto-material.json");
            var outputDir = Path.Combine(tempDir, "verification_output");

            var depJson = JsonSerializer.Serialize(export, BmfJsonOptions);
            await File.WriteAllTextAsync(depPath, depJson, cancellationToken).ConfigureAwait(false);

            var crypto = await GenerateCryptoMaterialAsync(cashRegisterId, cancellationToken).ConfigureAwait(false);
            RksvDepPrueftoolCryptoMaterialWriter.Write(crypto, cryptoPath);

            var runResult = _prueftoolRunner.RunCheckDepExport(depPath, cryptoPath, outputDir);
            var passed = runResult.ExitCode == 0
                         && string.Equals(runResult.VerificationState, "PASS", StringComparison.OrdinalIgnoreCase);
            var toolOutput = string.Join(
                Environment.NewLine,
                new[] { runResult.StdOut, runResult.StdErr }.Where(s => !string.IsNullOrWhiteSpace(s)));

            _logger.LogInformation(
                "DEP Prüftool finished for register {RegisterId}: exit={ExitCode} state={State} demo={IsDemo}",
                cashRegisterId,
                runResult.ExitCode,
                runResult.VerificationState,
                isDemo);

            return new RksvDepPrueftoolResult
            {
                Success = passed,
                IsDemo = isDemo,
                Skipped = false,
                Environment = environment,
                Message = passed
                    ? "Prüftool validation passed"
                    : "Prüftool validation failed",
                VerificationState = runResult.VerificationState,
                ToolOutput = string.IsNullOrWhiteSpace(toolOutput)
                    ? $"Exit code {runResult.ExitCode}"
                    : toolOutput,
                ValidatedAtUtc = validatedAt,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEP Prüftool validation failed for register {RegisterId}", cashRegisterId);
            return new RksvDepPrueftoolResult
            {
                Success = false,
                IsDemo = isDemo,
                Skipped = false,
                Environment = environment,
                Message = "Prüftool validation failed",
                ToolOutput = ex.Message,
                ValidatedAtUtc = validatedAt,
            };
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // best effort cleanup
            }
        }
    }

    private static void ValidateCertificateGroup(
        JsonElement group,
        int groupIndex,
        bool isDemo,
        List<string> errors,
        List<string> warnings,
        ref int belegCount)
    {
        if (!group.TryGetProperty("Signaturzertifikat", out var signaturzertifikat)
            || signaturzertifikat.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(signaturzertifikat.GetString()))
        {
            errors.Add($"Group {groupIndex}: missing or empty 'Signaturzertifikat'.");
        }

        if (!group.TryGetProperty("Zertifizierungsstellen", out var zertifizierungsstellen)
            || zertifizierungsstellen.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"Group {groupIndex}: missing 'Zertifizierungsstellen' array.");
        }
        else if (!isDemo && zertifizierungsstellen.GetArrayLength() == 0)
        {
            warnings.Add(
                $"Group {groupIndex}: empty 'Zertifizierungsstellen' — expected production PKI issuer chain.");
        }

        if (!group.TryGetProperty("Belege-kompakt", out var belegeKompakt)
            || belegeKompakt.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"Group {groupIndex}: missing 'Belege-kompakt' array.");
            return;
        }

        if (belegeKompakt.GetArrayLength() == 0)
        {
            errors.Add($"Group {groupIndex}: 'Belege-kompakt' must contain at least one compact JWS.");
            return;
        }

        var belegIndex = 0;
        foreach (var beleg in belegeKompakt.EnumerateArray())
        {
            belegIndex++;
            belegCount++;

            if (beleg.ValueKind != JsonValueKind.String)
            {
                errors.Add($"Group {groupIndex}, beleg {belegIndex}: 'Belege-kompakt' entries must be compact JWS strings.");
                continue;
            }

            if (!IsValidCompactJws(beleg.GetString()))
            {
                errors.Add($"Group {groupIndex}, beleg {belegIndex}: invalid compact JWS (expected header.payload.signature).");
            }
        }
    }

    private static readonly JsonSerializerOptions BmfJsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = true,
    };

    public async Task<CryptoMaterialDto> GenerateCryptoMaterialAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var registerExists = await _context.CashRegisters
            .AsNoTracking()
            .AnyAsync(c => c.Id == cashRegisterId, cancellationToken)
            .ConfigureAwait(false);
        if (!registerExists)
            throw new InvalidOperationException($"Cash register {cashRegisterId} not found.");

        var aesKey = _tseKeyProvider.GetTurnoverCounterAesKeyBytes();
        if (aesKey is null || aesKey.Length == 0)
            throw new InvalidOperationException("Turnover counter AES key is not configured.");

        var thumbprints = await CollectRegisterCertificateThumbprintsAsync(cashRegisterId, cancellationToken)
            .ConfigureAwait(false);

        var certificates = new List<CertificateInfoDto>();
        var seenSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var thumbprint in thumbprints)
        {
            var entry = await BuildCertificateInfoAsync(thumbprint, cancellationToken)
                .ConfigureAwait(false);
            if (entry is null || !seenSerials.Add(entry.SerialNumber))
                continue;

            certificates.Add(entry);
        }

        if (certificates.Count == 0)
        {
            var fallback = await BuildCertificateInfoAsync(
                    NormalizeThumbprint(_tseKeyProvider.GetCurrentCertificateThumbprint()),
                    cancellationToken)
                .ConfigureAwait(false);
            if (fallback is not null)
                certificates.Add(fallback);
        }

        var turnoverCounters = await BuildTurnoverCountersAsync(cashRegisterId, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "RKSV DEP crypto material: register={RegisterId} certificates={CertificateCount} turnoverEntries={TurnoverCount}",
            cashRegisterId,
            certificates.Count,
            turnoverCounters.Count);

        return new CryptoMaterialDto
        {
            AesKeyBase64 = Convert.ToBase64String(aesKey),
            Certificates = certificates,
            TurnoverCounters = turnoverCounters,
        };
    }

    private async Task<Dictionary<string, string>> BuildTurnoverCountersAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken)
    {
        var signedPayments = await _context.PaymentDetails
            .AsNoTracking()
            .Where(p =>
                p.CashRegisterId == cashRegisterId
                && p.TseSignature != null
                && p.TseSignature != "")
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.ReceiptNumber)
            .Select(p => new { p.ReceiptNumber, p.TotalAmount })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var turnoverCounters = new Dictionary<string, string>(StringComparer.Ordinal);
        long turnoverCents = 0;
        foreach (var payment in signedPayments)
        {
            if (string.IsNullOrWhiteSpace(payment.ReceiptNumber))
                continue;

            var grossCents = (long)Math.Round(payment.TotalAmount * 100m, MidpointRounding.AwayFromZero);
            if (grossCents != 0)
                turnoverCents += grossCents;

            turnoverCounters[payment.ReceiptNumber.Trim()] = turnoverCents.ToString();
        }

        return turnoverCounters;
    }

    private async Task<HashSet<string>> CollectRegisterCertificateThumbprintsAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken)
    {
        var paymentThumbprints = await _context.PaymentDetails
            .AsNoTracking()
            .Where(p =>
                p.CashRegisterId == cashRegisterId
                && p.TseSignature != null
                && p.TseSignature != "")
            .Select(p => p.CertificateThumbprint)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var closingThumbprints = await _context.DailyClosings
            .AsNoTracking()
            .Where(c =>
                c.CashRegisterId == cashRegisterId
                && c.TseSignature != null
                && c.TseSignature != "")
            .Select(c => c.CertificateThumbprint)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var thumbprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var thumbprint in paymentThumbprints.Concat(closingThumbprints))
            thumbprints.Add(NormalizeThumbprint(thumbprint));

        var current = NormalizeThumbprint(_tseKeyProvider.GetCurrentCertificateThumbprint());
        if (!string.IsNullOrEmpty(current) && !string.Equals(current, "UNKNOWN", StringComparison.Ordinal))
            thumbprints.Add(current);

        return thumbprints;
    }

    private async Task<CertificateInfoDto?> BuildCertificateInfoAsync(
        string thumbprint,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(thumbprint) || string.Equals(thumbprint, "UNKNOWN", StringComparison.Ordinal))
            return null;

        var certificate = await _tseKeyProvider
            .GetCertificateByThumbprintAsync(thumbprint, cancellationToken)
            .ConfigureAwait(false);
        if (certificate is null)
        {
            _logger.LogWarning("DEP crypto material: certificate not found for thumbprint {Thumbprint}", thumbprint);
            return null;
        }

        var derBytes = certificate.Export(X509ContentType.Cert);
        var serial = CmcParser.ParseCertificate(derBytes).SerialNumber;
        if (string.IsNullOrEmpty(serial))
            return null;

        return new CertificateInfoDto
        {
            SerialNumber = serial,
            CertificateDerBase64 = Convert.ToBase64String(derBytes),
            Thumbprint = thumbprint.Trim().ToUpperInvariant(),
        };
    }

    private async Task<List<RksvDepReceiptSignatureInfo>> GetReceiptsWithSignaturesAsync(
        Guid cashRegisterId,
        DateTime fromUtc,
        DateTime toUtc,
        bool includeSpecialReceipts,
        bool includeDailyClosings,
        CancellationToken cancellationToken)
    {
        var receipts = new List<RksvDepReceiptSignatureInfo>();
        var defaultThumb = NormalizeThumbprint(_tseKeyProvider.GetCurrentCertificateThumbprint());

        // 1. Normal payments (non-special fiscal rows)
        var normalPayments = await _context.PaymentDetails
            .AsNoTracking()
            .Where(p =>
                p.CashRegisterId == cashRegisterId
                && p.CreatedAt >= fromUtc
                && p.CreatedAt <= toUtc
                && p.RksvSpecialReceiptKind == null
                && p.TseSignature != null
                && p.TseSignature != "")
            .Select(p => new PaymentSignatureRow(
                p.TseSignature,
                p.CreatedAt,
                p.CertificateThumbprint,
                p.ReceiptNumber))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        receipts.AddRange(MapPaymentSignatureRows(normalPayments, RksvDepReceiptTypes.Normal));

        // 2. Special receipts (Startbeleg, Monatsbeleg, Jahresbeleg, Schlussbeleg, Nullbeleg)
        if (includeSpecialReceipts)
        {
            var specialReceipts = await _context.PaymentDetails
                .AsNoTracking()
                .Where(p =>
                    p.CashRegisterId == cashRegisterId
                    && p.RksvSpecialReceiptKind != null
                    && p.CreatedAt >= fromUtc
                    && p.CreatedAt <= toUtc
                    && p.TseSignature != null
                    && p.TseSignature != "")
                .Select(p => new PaymentSignatureRow(
                    p.TseSignature,
                    p.CreatedAt,
                    p.CertificateThumbprint,
                    p.ReceiptNumber,
                    p.RksvSpecialReceiptKind))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            receipts.AddRange(MapPaymentSignatureRows(specialReceipts, receiptTypeFromRow: true));
        }

        // 3. Daily closings
        if (includeDailyClosings)
        {
            var closings = await _context.DailyClosings
                .AsNoTracking()
                .Where(c =>
                    c.CashRegisterId == cashRegisterId
                    && c.ClosingDate >= fromUtc
                    && c.ClosingDate <= toUtc
                    && c.TseSignature != null
                    && c.TseSignature != "")
                .Select(c => new { c.TseSignature, c.ClosingDate, c.CertificateThumbprint })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            receipts.AddRange(
                closings
                    .Where(c => IsValidCompactJws(c.TseSignature))
                    .Select(c => new RksvDepReceiptSignatureInfo
                    {
                        TseSignature = c.TseSignature.Trim(),
                        IssuedAt = NormalizeUtc(c.ClosingDate),
                        CertificateThumbprint = NormalizeThumbprint(c.CertificateThumbprint ?? defaultThumb),
                        ReceiptType = RksvDepReceiptTypes.DailyClosing,
                        SequenceNumber = ResolveDailyClosingSequenceNumber(c.ClosingDate, c.ClosingDate),
                    }));
        }

        return OrderReceiptsForDepExport(receipts);
    }

    private IEnumerable<RksvDepReceiptSignatureInfo> MapPaymentSignatureRows(
        IEnumerable<PaymentSignatureRow> rows,
        string? fixedReceiptType = null,
        bool receiptTypeFromRow = false)
    {
        foreach (var row in rows)
        {
            if (!IsValidCompactJws(row.TseSignature))
                continue;

            yield return new RksvDepReceiptSignatureInfo
            {
                TseSignature = row.TseSignature.Trim(),
                IssuedAt = row.CreatedAt,
                CertificateThumbprint = NormalizeThumbprint(row.CertificateThumbprint),
                ReceiptType = receiptTypeFromRow
                    ? MapPaymentReceiptType(row.RksvSpecialReceiptKind)
                    : fixedReceiptType ?? RksvDepReceiptTypes.Normal,
                SequenceNumber = ResolvePaymentSequenceNumber(row.ReceiptNumber, row.CreatedAt),
            };
        }
    }

    private sealed record PaymentSignatureRow(
        string TseSignature,
        DateTime CreatedAt,
        string? CertificateThumbprint,
        string ReceiptNumber,
        string? RksvSpecialReceiptKind = null);

    internal static List<RksvDepReceiptSignatureInfo> OrderReceiptsForDepExport(
        IEnumerable<RksvDepReceiptSignatureInfo> receipts) =>
        receipts
            .OrderBy(r => r.IssuedAt)
            .ThenBy(r => r.SequenceNumber)
            .ThenBy(r => r.ReceiptType, StringComparer.Ordinal)
            .ToList();

    private static string MapPaymentReceiptType(string? specialKind) =>
        string.IsNullOrWhiteSpace(specialKind)
            ? RksvDepReceiptTypes.Normal
            : specialKind.Trim();

    private static int ResolvePaymentSequenceNumber(string? receiptNumber, DateTime issuedAtUtc)
    {
        if (TseChainContinuityAnalyzer.TryParseBelegNrSequence(receiptNumber ?? string.Empty, out var sequence, out _))
            return sequence;

        return (int)(issuedAtUtc.Ticks / TimeSpan.TicksPerSecond);
    }

    private static int ResolveDailyClosingSequenceNumber(DateTime closingDate, DateTime issuedAtUtc)
    {
        var localDate = closingDate.Date;
        if (localDate.Year >= 2000)
            return localDate.Year * 10_000 + localDate.Month * 100 + localDate.Day;

        return (int)(issuedAtUtc.Ticks / TimeSpan.TicksPerSecond);
    }

    private string NormalizeThumbprint(string? thumbprint)
    {
        if (!string.IsNullOrWhiteSpace(thumbprint))
            return thumbprint.Trim().ToUpperInvariant();

        var current = _tseKeyProvider.GetCurrentCertificateThumbprint();
        return string.IsNullOrWhiteSpace(current)
            ? "UNKNOWN"
            : current.Trim().ToUpperInvariant();
    }

    private async Task<string> GetCertificateBase64Async(
        string thumbprint,
        CancellationToken cancellationToken)
    {
        var certificate = await _tseKeyProvider
            .GetCertificateByThumbprintAsync(thumbprint, cancellationToken)
            .ConfigureAwait(false);
        if (certificate == null)
        {
            _logger.LogWarning("DEP export: certificate not found for thumbprint {Thumbprint}", thumbprint);
            return string.Empty;
        }

        var derBytes = certificate.Export(X509ContentType.Cert);
        return Convert.ToBase64String(derBytes);
    }

    private async Task<List<string>> GetCertificateChainBase64Async(
        string thumbprint,
        CancellationToken cancellationToken)
    {
        var chain = await _tseKeyProvider
            .GetCertificateChainAsync(thumbprint, cancellationToken)
            .ConfigureAwait(false);

        return TseCertificateChainBuilder.ToBase64DerList(chain).ToList();
    }

    /// <summary>JWS compact: exactly three Base64URL segments separated by '.'.</summary>
    internal static bool IsValidCompactJws(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Trim().Split('.');
        return parts.Length == 3 && parts.All(p => !string.IsNullOrEmpty(p));
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
