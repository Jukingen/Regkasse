using System.Globalization;
using System.Text.Json;
using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Fiscal;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// RKSV Sonderbelege (Nullbeleg first). Does not use <see cref="PaymentService"/> / POS <see cref="DTOs.CreatePaymentRequest"/>.
/// </summary>
/// <remarks>
/// TODO (legal): Whether Nullbeleg must be submitted to FinanzOnline / RKSV web service and exact BMF Belegcheck payload rules.
/// TODO (legal): Confirm Monats-Nullbeleg timestamp (last second of month vs issuance time) with fiscal advisor.
/// </remarks>
public sealed class RksvSpecialReceiptService : IRksvSpecialReceiptService
{
    private readonly AppDbContext _db;
    private readonly ITseService _tseService;
    private readonly IReceiptSequenceService _receiptSequence;
    private readonly IReceiptService _receiptService;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly CompanyProfileOptions _companyProfile;
    private readonly TseOptions _tseOptions;
    private readonly ILogger<RksvSpecialReceiptService> _logger;

    public RksvSpecialReceiptService(
        AppDbContext db,
        ITseService tseService,
        IReceiptSequenceService receiptSequence,
        IReceiptService receiptService,
        ISettingsTenantResolver tenantResolver,
        IOptions<CompanyProfileOptions> companyProfile,
        IOptions<TseOptions> tseOptions,
        ILogger<RksvSpecialReceiptService> logger)
    {
        _db = db;
        _tseService = tseService;
        _receiptSequence = receiptSequence;
        _receiptService = receiptService;
        _tenantResolver = tenantResolver;
        _companyProfile = companyProfile.Value;
        _tseOptions = tseOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateNullbelegResponse> CreateNullbelegAsync(
        CreateNullbelegRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("Actor user id is required.", nameof(actorUserId));

        await EnsureTseReadyForNullbelegAsync(cancellationToken).ConfigureAwait(false);

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var register = await _db.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == request.CashRegisterId && r.TenantId == tenantId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Cash register not found for the current tenant.");

        var duplicate = await _db.PaymentDetails.AsNoTracking()
            .AnyAsync(
                p => p.CashRegisterId == request.CashRegisterId &&
                     p.IsActive &&
                     p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Nullbeleg &&
                     p.RksvSpecialReceiptYear == request.Year &&
                     p.RksvSpecialReceiptMonth == request.Month,
                cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
        {
            throw new InvalidOperationException(
                $"A Nullbeleg already exists for register {register.RegisterNumber} in {request.Year}-{request.Month:00}.");
        }

        var guest = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == WalkInCustomerConstants.GuestCustomerId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Guest customer is not available; cannot create Nullbeleg.");

        var lastDayOfMonth = new DateTime(request.Year, request.Month, DateTime.DaysInMonth(request.Year, request.Month), 23, 59, 59, DateTimeKind.Unspecified);
        var issuedAtUtc = TimeZoneInfo.ConvertTimeToUtc(lastDayOfMonth, PostgreSqlUtcDateTime.AustriaTimeZone);
        var sequenceAnchor = new DateTime(request.Year, request.Month, DateTime.DaysInMonth(request.Year, request.Month), 0, 0, 0, DateTimeKind.Unspecified);
        var actsAsJahres = request.ActsAsJahresbeleg ?? (request.Month == 12);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var receiptNumber = await _receiptSequence.AllocateNextBelegNrInTransactionAsync(
                    tx,
                    request.CashRegisterId,
                    register.RegisterNumber,
                    sequenceAnchor)
                .ConfigureAwait(false);

            var taxDetailsJson = "{}";
            var sig = await FiscalTseSigning.SignAsync(
                    _tseService,
                    new FiscalSigningRequest(
                        request.CashRegisterId,
                        receiptNumber,
                        0m,
                        register.RegisterNumber,
                        PrevSignatureValue: null,
                        Timestamp: issuedAtUtc,
                        TaxDetailsJson: taxDetailsJson,
                        DbTransaction: tx))
                .ConfigureAwait(false);

            var paymentId = Guid.NewGuid();
            var payment = new PaymentDetails
            {
                Id = paymentId,
                CustomerId = guest.Id,
                CustomerName = guest.Name,
                TableNumber = 0,
                CashierId = actorUserId,
                TotalAmount = 0m,
                TaxAmount = 0m,
                PaymentMethodRaw = ((int)PaymentMethod.Cash).ToString(CultureInfo.InvariantCulture),
                Steuernummer = _companyProfile.TaxNumber,
                CashRegisterId = request.CashRegisterId,
                Notes = TruncateNotes(request.Reason),
                TseSignature = sig.CompactJws,
                PrevSignatureValueUsed = sig.PrevSignatureValueUsed,
                TseTimestamp = issuedAtUtc,
                TaxDetails = JsonDocument.Parse("{}"),
                PaymentItems = JsonDocument.Parse("[]"),
                ReceiptNumber = receiptNumber,
                IsPrinted = false,
                RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Nullbeleg,
                RksvSpecialReceiptYear = request.Year,
                RksvSpecialReceiptMonth = request.Month,
                RksvNullbelegActsAsJahresbeleg = actsAsJahres,
                CreatedAt = issuedAtUtc,
                CreatedBy = actorUserId,
                IsActive = true,
            };

            var companyAddress = $"{_companyProfile.Street}, {_companyProfile.ZipCode} {_companyProfile.City}";
            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                SourcePaymentId = paymentId,
                InvoiceNumber = receiptNumber,
                InvoiceDate = issuedAtUtc,
                DueDate = issuedAtUtc,
                Status = InvoiceStatus.Paid,
                Subtotal = 0m,
                TaxAmount = 0m,
                TotalAmount = 0m,
                PaidAmount = 0m,
                RemainingAmount = 0m,
                CustomerName = guest.Name,
                CustomerTaxNumber = payment.Steuernummer,
                CompanyName = _companyProfile.CompanyName,
                CompanyTaxNumber = _companyProfile.TaxNumber,
                CompanyAddress = companyAddress,
                TseSignature = payment.TseSignature,
                KassenId = register.RegisterNumber,
                TseTimestamp = payment.TseTimestamp,
                CashRegisterId = request.CashRegisterId,
                PaymentMethod = PaymentMethod.Cash,
                PaymentReference = null,
                PaymentDate = issuedAtUtc,
                InvoiceItems = JsonDocument.Parse("[]"),
                TaxDetails = JsonDocument.Parse("{}"),
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            };

            _db.PaymentDetails.Add(payment);
            _db.Invoices.Add(invoice);
            await _receiptService.AddReceiptFromPaymentToContextAsync(payment).ConfigureAwait(false);

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            var receiptEntity = await _db.Receipts.AsNoTracking()
                .FirstAsync(r => r.PaymentId == paymentId, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Nullbeleg created PaymentId={PaymentId} ReceiptNumber={ReceiptNumber} Register={Register} Period={Y}-{M}",
                paymentId, receiptNumber, register.RegisterNumber, request.Year, request.Month);

            return new CreateNullbelegResponse
            {
                PaymentId = paymentId,
                InvoiceId = invoice.Id,
                ReceiptId = receiptEntity.ReceiptId,
                ReceiptNumber = receiptNumber,
                ActsAsJahresbeleg = actsAsJahres,
            };
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static string? TruncateNotes(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return null;
        var t = reason.Trim();
        return t.Length <= 500 ? t : t[..500];
    }

    private async Task EnsureTseReadyForNullbelegAsync(CancellationToken cancellationToken)
    {
        if (_tseOptions.IsOff)
            throw new InvalidOperationException("TSE is disabled (TseMode=Off); Nullbeleg cannot be signed.");

        if (_tseOptions.UseSoftTseWhenNoDevice)
            return;

        var st = await _tseService.GetDeviceStatusAsync().ConfigureAwait(false);
        if (!st.IsConnected)
            throw new InvalidOperationException("TSE device is not connected; Nullbeleg signing requires a connected TSE.");
        if (!st.IsReady)
            throw new InvalidOperationException($"TSE device is not ready (status: {st.Status}); cannot sign Nullbeleg.");
    }
}
