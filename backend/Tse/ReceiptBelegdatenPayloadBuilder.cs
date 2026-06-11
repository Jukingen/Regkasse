using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Tse;

/// <summary>
/// Reconstructs RKSV §9 Belegdaten from persisted payment / register context.
/// </summary>
public sealed class ReceiptBelegdatenPayloadBuilder : IBelegdatenPayloadBuilder
{
    private readonly AppDbContext _context;
    private readonly ITseKeyProvider _keyProvider;

    public ReceiptBelegdatenPayloadBuilder(AppDbContext context, ITseKeyProvider keyProvider)
    {
        _context = context;
        _keyProvider = keyProvider;
    }

    public async Task<string?> TryGetCompactJwsAsync(
        Guid cashRegisterId,
        string receiptNumber,
        CancellationToken cancellationToken = default)
    {
        if (cashRegisterId == Guid.Empty || string.IsNullOrWhiteSpace(receiptNumber))
            return null;

        var signature = await _context.PaymentDetails
            .AsNoTracking()
            .Where(p =>
                p.CashRegisterId == cashRegisterId
                && p.ReceiptNumber == receiptNumber.Trim())
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => p.TseSignature)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(signature) ? null : signature.Trim();
    }

    public async Task<BelegdatenPayload> BuildAsync(
        Guid cashRegisterId,
        string receiptNumber,
        DateTime issuedAt,
        CancellationToken cancellationToken = default)
    {
        if (cashRegisterId == Guid.Empty)
            throw new ArgumentException("cashRegisterId must not be empty.", nameof(cashRegisterId));
        if (string.IsNullOrWhiteSpace(receiptNumber))
            throw new ArgumentException("receiptNumber is required.", nameof(receiptNumber));

        var normalizedReceiptNumber = receiptNumber.Trim();
        var issuedAtUtc = NormalizeUtc(issuedAt);

        var payment = await FindPaymentAsync(cashRegisterId, normalizedReceiptNumber, cancellationToken)
            .ConfigureAwait(false);

        if (payment == null)
            throw new InvalidOperationException(
                $"Payment not found for register {cashRegisterId} and receipt {normalizedReceiptNumber}.");

        var registerNumber = await _context.CashRegisters
            .AsNoTracking()
            .Where(r => r.Id == cashRegisterId)
            .Select(r => r.RegisterNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(registerNumber))
            throw new InvalidOperationException($"Cash register {cashRegisterId} not found.");

        var taxDetailsJson = payment.TaxDetails.RootElement.ValueKind == JsonValueKind.Undefined
            ? null
            : payment.TaxDetails.RootElement.GetRawText();
        var taxSets = BelegdatenPayloadBuilder.MapTaxSets(taxDetailsJson, payment.TotalAmount);
        var turnoverCents = await ComputeTurnoverCentsAtReceiptAsync(
                cashRegisterId,
                normalizedReceiptNumber,
                cancellationToken)
            .ConfigureAwait(false);

        var aesKey = _keyProvider.GetTurnoverCounterAesKeyBytes()
            ?? throw new InvalidOperationException("Turnover counter AES key is not configured.");

        var certSerial = await ResolveCertificateSerialAsync(payment.CertificateThumbprint, cancellationToken)
            .ConfigureAwait(false);

        var timestampUtc = payment.TseTimestamp != default
            ? NormalizeUtc(payment.TseTimestamp)
            : issuedAtUtc;

        return BelegdatenPayloadBuilder.Build(
            registerNumber.Trim(),
            normalizedReceiptNumber,
            timestampUtc,
            taxSets,
            turnoverCents,
            string.IsNullOrWhiteSpace(payment.PrevSignatureValueUsed) ? null : payment.PrevSignatureValueUsed,
            certSerial,
            aesKey,
            updateTurnoverCounter: taxSets.TotalGrossCents != 0);
    }

    private async Task<long> ComputeTurnoverCentsAtReceiptAsync(
        Guid cashRegisterId,
        string receiptNumber,
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

        long turnoverCents = 0;
        foreach (var row in signedPayments)
        {
            var grossCents = (long)Math.Round(row.TotalAmount * 100m, MidpointRounding.AwayFromZero);
            if (grossCents != 0)
                turnoverCents += grossCents;

            if (string.Equals(row.ReceiptNumber?.Trim(), receiptNumber, StringComparison.Ordinal))
                break;
        }

        return turnoverCents;
    }

    private async Task<string> ResolveCertificateSerialAsync(
        string? certificateThumbprint,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(certificateThumbprint))
        {
            var cert = await _keyProvider
                .GetCertificateByThumbprintAsync(certificateThumbprint.Trim(), cancellationToken)
                .ConfigureAwait(false);
            if (cert != null)
            {
                using (cert)
                {
                    var parsed = CmcParser.ParseCertificate(cert.RawData);
                    if (!string.IsNullOrWhiteSpace(parsed.SerialNumber))
                        return parsed.SerialNumber;
                }
            }
        }

        return _keyProvider.GetCertificateSerialNumber() ?? "UNKNOWN";
    }

    private Task<PaymentDetails?> FindPaymentAsync(
        Guid cashRegisterId,
        string receiptNumber,
        CancellationToken cancellationToken) =>
        _context.PaymentDetails
            .AsNoTracking()
            .Where(p =>
                p.CashRegisterId == cashRegisterId
                && p.ReceiptNumber == receiptNumber)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
