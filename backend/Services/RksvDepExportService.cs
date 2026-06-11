using System.Security.Cryptography.X509Certificates;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public class RksvDepExportService : IRksvDepExportService
{
    private static readonly TimeSpan MaxPeriod = TimeSpan.FromDays(366);

    private readonly AppDbContext _context;
    private readonly ITseKeyProvider _tseKeyProvider;
    private readonly ILogger<RksvDepExportService> _logger;

    public RksvDepExportService(
        AppDbContext context,
        ITseKeyProvider tseKeyProvider,
        ILogger<RksvDepExportService> logger)
    {
        _context = context;
        _tseKeyProvider = tseKeyProvider;
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
