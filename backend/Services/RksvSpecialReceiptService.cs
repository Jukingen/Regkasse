using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Fiscal;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
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
/// TODO (legal): Startbeleg — BMF Belegcheck / FinanzOnline submission deadlines and payload fields vs normal Beleg.
/// TODO (legal): BMF Belegcheck / FinanzOnline submission deadlines and payload rules for Jahresbeleg (not implemented in-app).
/// TODO (legal): Early Jahresbeleg and continued sales — operator process only; this codebase does not enforce post-Jahresbeleg sales stops.
/// TODO (security): Schlussbeleg / Endbeleg — add operator password or second-factor confirmation when a register credential model exists (not invented here).
/// </remarks>
public sealed class RksvSpecialReceiptService : IRksvSpecialReceiptService
{
    private readonly AppDbContext _db;
    private readonly ITseService _tseService;
    private readonly IReceiptSequenceService _receiptSequence;
    private readonly IReceiptService _receiptService;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly ICompanyProfileProvider _companyProfileProvider;
    private readonly TseOptions _tseOptions;
    private readonly ILogger<RksvSpecialReceiptService> _logger;
    private readonly IRksvSpecialReceiptFinanzOnlineSubmissionTracker _fonSubmissionTracker;
    private readonly IFinanzOnlineOutboxService _finanzOnlineOutbox;

    public RksvSpecialReceiptService(
        AppDbContext db,
        ITseService tseService,
        IReceiptSequenceService receiptSequence,
        IReceiptService receiptService,
        ISettingsTenantResolver tenantResolver,
        ICompanyProfileProvider companyProfileProvider,
        IOptions<TseOptions> tseOptions,
        ILogger<RksvSpecialReceiptService> logger,
        IRksvSpecialReceiptFinanzOnlineSubmissionTracker fonSubmissionTracker,
        IFinanzOnlineOutboxService finanzOnlineOutbox)
    {
        _db = db;
        _tseService = tseService;
        _receiptSequence = receiptSequence;
        _receiptService = receiptService;
        _tenantResolver = tenantResolver;
        _companyProfileProvider = companyProfileProvider;
        _tseOptions = tseOptions.Value;
        _logger = logger;
        _fonSubmissionTracker = fonSubmissionTracker;
        _finanzOnlineOutbox = finanzOnlineOutbox;
    }

    private static readonly JsonSerializerOptions RksvFonOutboxJsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc />
    public async Task<CreateNullbelegResponse> CreateNullbelegAsync(
        CreateNullbelegRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("Actor user id is required.", nameof(actorUserId));

        var (_, viennaCurrentMonth) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        var resolvedMonth = request.Month ?? viennaCurrentMonth;

        await EnsureTseReadyForSignedSpecialReceiptAsync(cancellationToken).ConfigureAwait(false);

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var companyProfile = await _companyProfileProvider.GetCompanyProfileAsync(cancellationToken).ConfigureAwait(false);
        var register = await _db.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == request.CashRegisterId && r.TenantId == tenantId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Cash register not found for the current tenant.");

        EnsureRegisterNotDecommissioned(register);

        var duplicate = await _db.PaymentDetails.AsNoTracking()
            .AnyAsync(
                p => p.CashRegisterId == request.CashRegisterId &&
                     p.IsActive &&
                     p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Nullbeleg &&
                     p.RksvSpecialReceiptYear == request.Year &&
                     p.RksvSpecialReceiptMonth == resolvedMonth,
                cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
        {
            throw new InvalidOperationException(
                $"A Nullbeleg already exists for register {register.RegisterNumber} in {request.Year}-{resolvedMonth:00}.");
        }

        var guest = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == WalkInCustomerConstants.GuestCustomerId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Guest customer is not available; cannot create Nullbeleg.");

        var lastDayOfMonth = new DateTime(request.Year, resolvedMonth, DateTime.DaysInMonth(request.Year, resolvedMonth), 23, 59, 59, DateTimeKind.Unspecified);
        var issuedAtUtc = TimeZoneInfo.ConvertTimeToUtc(lastDayOfMonth, PostgreSqlUtcDateTime.AustriaTimeZone);
        var sequenceAnchor = new DateTime(request.Year, resolvedMonth, DateTime.DaysInMonth(request.Year, resolvedMonth), 0, 0, 0, DateTimeKind.Unspecified);
        var actsAsJahres = request.ActsAsJahresbeleg ?? (resolvedMonth == 12);

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
                Steuernummer = companyProfile.TaxNumber,
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
                RksvSpecialReceiptMonth = resolvedMonth,
                RksvNullbelegActsAsJahresbeleg = actsAsJahres,
                CreatedAt = issuedAtUtc,
                CreatedBy = actorUserId,
                IsActive = true,
            };

            var companyAddress = $"{companyProfile.Street}, {companyProfile.ZipCode} {companyProfile.City}";
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
                CompanyName = companyProfile.CompanyName,
                CompanyTaxNumber = companyProfile.TaxNumber,
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
                paymentId, receiptNumber, register.RegisterNumber, request.Year, resolvedMonth);

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

    private static string? BuildJahresbelegNotes(string? reason, string? earlyReason)
    {
        var r = TruncateNotes(reason);
        var e = TruncateNotes(earlyReason);
        if (r == null && e == null)
            return null;
        if (r != null && e != null)
        {
            var combined = $"{r} | Early: {e}";
            return combined.Length <= 500 ? combined : combined[..500];
        }

        return r ?? TruncateNotes($"Early: {e}");
    }

    private static void EnsureRegisterNotDecommissioned(CashRegister register)
    {
        if (register.Status == RegisterStatus.Decommissioned)
        {
            throw new RksvOperationGuardException(
                RksvGuardErrorCodes.RegisterDecommissioned,
                $"Cash register {register.RegisterNumber} is permanently decommissioned (RKSV Schlussbeleg) and cannot receive new special receipts.");
        }
    }

    private async Task<bool> GetDecemberMonatsbelegCountsAsJahresbelegAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var flag = await _db.CompanySettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => (bool?)s.UseDecemberMonatsbelegAsJahresbeleg)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return flag ?? true;
    }

    private Guid ResolveReceiptIdFromChangeTracker(Guid paymentId)
    {
        foreach (var e in _db.ChangeTracker.Entries<Receipt>())
        {
            if (e.Entity.PaymentId == paymentId)
                return e.Entity.ReceiptId;
        }

        throw new InvalidOperationException($"Receipt for PaymentId={paymentId} was not found in the EF change tracker.");
    }

    private static string GetQrPayloadFromChangeTracker(AppDbContext db, Guid paymentId)
    {
        foreach (var e in db.ChangeTracker.Entries<Receipt>())
        {
            if (e.Entity.PaymentId == paymentId)
                return e.Entity.QrCodePayload ?? string.Empty;
        }

        return string.Empty;
    }

    private static string ComputeSha256HexForOutbox(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task EnqueueRksvSpecialReceiptFinanzOnlineOutboxAsync(
        string messageType,
        Guid tenantId,
        string registerNumber,
        Guid paymentId,
        Guid receiptId,
        Guid cashRegisterId,
        string receiptNumber,
        string kind,
        CancellationToken cancellationToken)
    {
        var inner = new RksvSpecialReceiptFinanzOnlineOutboxPayloadBody
        {
            Kind = kind,
            PaymentId = paymentId,
            ReceiptId = receiptId,
            CashRegisterId = cashRegisterId,
            ReceiptNumber = receiptNumber,
            QrPayload = GetQrPayloadFromChangeTracker(_db, paymentId),
        };
        var innerJson = JsonSerializer.Serialize(inner, RksvFonOutboxJsonOpts);
        var payloadHashHex = ComputeSha256HexForOutbox(innerJson);
        var businessKey = $"rksv|{receiptId:N}|{kind}";
        await _finanzOnlineOutbox.EnqueueSubmissionAsync(
            aggregateType: "RksvSpecialReceipt",
            aggregateId: receiptId,
            messageType: messageType,
            businessKey: businessKey,
            payload: new FinanzOnlineOutboxPayload
            {
                Mode = FinanzOnlineIntegrationMode.TEST,
                Scope = new FinanzOnlineScope
                {
                    TenantId = tenantId.ToString("N"),
                    RegisterId = registerNumber,
                },
                Correlation = new FinanzOnlineCorrelationContext
                {
                    BusinessKey = businessKey,
                    PayloadHash = payloadHashHex,
                    CorrelationId = paymentId.ToString("N"),
                },
                SubmissionKind = FinanzOnlineSubmissionKind.Register,
                PayloadJson = innerJson,
            },
            cancellationToken,
            persistImmediately: false).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CreateStartbelegResponse> CreateStartbelegAsync(
        CreateStartbelegRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("Actor user id is required.", nameof(actorUserId));

        await EnsureTseReadyForSignedSpecialReceiptAsync(cancellationToken).ConfigureAwait(false);

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var companyProfile = await _companyProfileProvider.GetCompanyProfileAsync(cancellationToken).ConfigureAwait(false);
        var register = await _db.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == request.CashRegisterId && r.TenantId == tenantId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Cash register not found for the current tenant.");

        EnsureRegisterNotDecommissioned(register);

        var duplicate = await _db.PaymentDetails.AsNoTracking()
            .AnyAsync(
                p => p.CashRegisterId == request.CashRegisterId &&
                     p.IsActive &&
                     p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Startbeleg,
                cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
        {
            throw new RksvOperationGuardException(
                RksvGuardErrorCodes.DuplicateStartbeleg,
                $"A Startbeleg already exists for register {register.RegisterNumber}.");
        }

        var guest = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == WalkInCustomerConstants.GuestCustomerId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Guest customer is not available; cannot create Startbeleg.");

        var viennaLocalNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PostgreSqlUtcDateTime.AustriaTimeZone);
        var issuedAtUnspecified = DateTime.SpecifyKind(viennaLocalNow, DateTimeKind.Unspecified);
        var issuedAtUtc = TimeZoneInfo.ConvertTimeToUtc(issuedAtUnspecified, PostgreSqlUtcDateTime.AustriaTimeZone);
        var sequenceAnchor = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();

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
            var correlation = TruncateCorrelation(request.CorrelationId);
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
                Steuernummer = companyProfile.TaxNumber,
                CashRegisterId = request.CashRegisterId,
                Notes = TruncateNotes(request.Reason),
                CorrelationId = correlation,
                TseSignature = sig.CompactJws,
                PrevSignatureValueUsed = sig.PrevSignatureValueUsed,
                TseTimestamp = issuedAtUtc,
                TaxDetails = JsonDocument.Parse("{}"),
                PaymentItems = JsonDocument.Parse("[]"),
                ReceiptNumber = receiptNumber,
                IsPrinted = false,
                RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Startbeleg,
                RksvSpecialReceiptYear = null,
                RksvSpecialReceiptMonth = null,
                RksvNullbelegActsAsJahresbeleg = false,
                CreatedAt = issuedAtUtc,
                CreatedBy = actorUserId,
                IsActive = true,
            };

            var companyAddress = $"{companyProfile.Street}, {companyProfile.ZipCode} {companyProfile.City}";
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
                CompanyName = companyProfile.CompanyName,
                CompanyTaxNumber = companyProfile.TaxNumber,
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

            var receiptIdForTracking = ResolveReceiptIdFromChangeTracker(paymentId);
            _db.RksvSpecialReceiptFinanzOnlineSubmissions.Add(
                _fonSubmissionTracker.CreateInitialNotRequiredRow(
                    paymentId,
                    receiptIdForTracking,
                    request.CashRegisterId,
                    RksvSpecialReceiptKinds.Startbeleg));

            await EnqueueRksvSpecialReceiptFinanzOnlineOutboxAsync(
                FinanzOnlineRksvSpecialReceiptOutboxMessageTypes.RksvStartbelegSubmission,
                tenantId,
                register.RegisterNumber,
                paymentId,
                receiptIdForTracking,
                request.CashRegisterId,
                receiptNumber,
                RksvSpecialReceiptKinds.Startbeleg,
                cancellationToken).ConfigureAwait(false);

            var cashRegStart = await _db.CashRegisters
                .FirstAsync(r => r.Id == request.CashRegisterId, cancellationToken)
                .ConfigureAwait(false);
            cashRegStart.StartbelegCreatedAt = issuedAtUtc;

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            var receiptEntity = await _db.Receipts.AsNoTracking()
                .FirstAsync(r => r.PaymentId == paymentId, cancellationToken)
                .ConfigureAwait(false);

            var dto = await _receiptService.GetReceiptByPaymentIdAsync(paymentId).ConfigureAwait(false);
            var qr = dto?.Signature?.QrData ?? string.Empty;

            _logger.LogInformation(
                "Startbeleg created PaymentId={PaymentId} ReceiptNumber={ReceiptNumber} Register={Register}",
                paymentId, receiptNumber, register.RegisterNumber);

            return new CreateStartbelegResponse
            {
                PaymentId = paymentId,
                InvoiceId = invoice.Id,
                ReceiptId = receiptEntity.ReceiptId,
                ReceiptNumber = receiptNumber,
                QrData = qr,
            };
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CreateMonatsbelegResponse> CreateMonatsbelegAsync(
        CreateMonatsbelegRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("Actor user id is required.", nameof(actorUserId));

        var (viennaYear, viennaMonth) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        if (request.Year != viennaYear || request.Month != viennaMonth)
        {
            throw new InvalidOperationException(
                $"Monatsbeleg can only be created for the current Vienna calendar month ({viennaYear}-{viennaMonth:00}), not {request.Year}-{request.Month:00}.");
        }

        var tenantIdForDecember = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);

        // December: Jahresbeleg flow vs separate Monatsbeleg row is controlled by tenant company settings.
        if (viennaMonth == 12 && await GetDecemberMonatsbelegCountsAsJahresbelegAsync(tenantIdForDecember, cancellationToken).ConfigureAwait(false))
        {
            var jResp = await CreateJahresbelegAsync(
                    new CreateJahresbelegRequest
                    {
                        CashRegisterId = request.CashRegisterId,
                        Year = viennaYear,
                        Reason = request.Reason,
                        EarlyReason = null,
                    },
                    actorUserId,
                    cancellationToken)
                .ConfigureAwait(false);
            return new CreateMonatsbelegResponse
            {
                PaymentId = jResp.PaymentId,
                InvoiceId = jResp.InvoiceId,
                ReceiptId = jResp.ReceiptId,
                ReceiptNumber = jResp.ReceiptNumber,
                QrData = jResp.QrData,
            };
        }

        await EnsureTseReadyForSignedSpecialReceiptAsync(cancellationToken).ConfigureAwait(false);

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var companyProfile = await _companyProfileProvider.GetCompanyProfileAsync(cancellationToken).ConfigureAwait(false);
        var register = await _db.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == request.CashRegisterId && r.TenantId == tenantId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Cash register not found for the current tenant.");

        EnsureRegisterNotDecommissioned(register);

        var duplicate = await _db.PaymentDetails.AsNoTracking()
            .AnyAsync(
                p => p.CashRegisterId == request.CashRegisterId &&
                     p.IsActive &&
                     p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg &&
                     p.RksvSpecialReceiptYear == request.Year &&
                     p.RksvSpecialReceiptMonth == request.Month,
                cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
        {
            throw new RksvOperationGuardException(
                RksvGuardErrorCodes.DuplicateMonatsbeleg,
                $"A Monatsbeleg already exists for register {register.RegisterNumber} in {request.Year}-{request.Month:00}.");
        }

        var guest = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == WalkInCustomerConstants.GuestCustomerId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Guest customer is not available; cannot create Monatsbeleg.");

        var viennaLocalNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PostgreSqlUtcDateTime.AustriaTimeZone);
        var issuedAtUnspecified = DateTime.SpecifyKind(viennaLocalNow, DateTimeKind.Unspecified);
        var issuedAtUtc = TimeZoneInfo.ConvertTimeToUtc(issuedAtUnspecified, PostgreSqlUtcDateTime.AustriaTimeZone);
        var sequenceAnchor = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();

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
                Steuernummer = companyProfile.TaxNumber,
                CashRegisterId = request.CashRegisterId,
                Notes = TruncateNotes(request.Reason),
                TseSignature = sig.CompactJws,
                PrevSignatureValueUsed = sig.PrevSignatureValueUsed,
                TseTimestamp = issuedAtUtc,
                TaxDetails = JsonDocument.Parse("{}"),
                PaymentItems = JsonDocument.Parse("[]"),
                ReceiptNumber = receiptNumber,
                IsPrinted = false,
                RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Monatsbeleg,
                RksvSpecialReceiptYear = request.Year,
                RksvSpecialReceiptMonth = request.Month,
                RksvNullbelegActsAsJahresbeleg = false,
                CreatedAt = issuedAtUtc,
                CreatedBy = actorUserId,
                IsActive = true,
            };

            var companyAddress = $"{companyProfile.Street}, {companyProfile.ZipCode} {companyProfile.City}";
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
                CompanyName = companyProfile.CompanyName,
                CompanyTaxNumber = companyProfile.TaxNumber,
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

            var cashRegMonatsbeleg = await _db.CashRegisters
                .FirstAsync(r => r.Id == request.CashRegisterId, cancellationToken)
                .ConfigureAwait(false);
            cashRegMonatsbeleg.LastMonatsbelegUtc = issuedAtUtc;

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            var receiptEntity = await _db.Receipts.AsNoTracking()
                .FirstAsync(r => r.PaymentId == paymentId, cancellationToken)
                .ConfigureAwait(false);

            var dto = await _receiptService.GetReceiptByPaymentIdAsync(paymentId).ConfigureAwait(false);
            var qr = dto?.Signature?.QrData ?? string.Empty;

            _logger.LogInformation(
                "Monatsbeleg created PaymentId={PaymentId} ReceiptNumber={ReceiptNumber} Register={Register} Period={Y}-{M}",
                paymentId, receiptNumber, register.RegisterNumber, request.Year, request.Month);

            return new CreateMonatsbelegResponse
            {
                PaymentId = paymentId,
                InvoiceId = invoice.Id,
                ReceiptId = receiptEntity.ReceiptId,
                ReceiptNumber = receiptNumber,
                QrData = qr,
            };
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CreateJahresbelegResponse> CreateJahresbelegAsync(
        CreateJahresbelegRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("Actor user id is required.", nameof(actorUserId));

        var (viennaYear, _) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        if (request.Year < viennaYear - 1 || request.Year > viennaYear)
        {
            throw new InvalidOperationException(
                $"Jahresbeleg can only be created for Vienna year {viennaYear} or {viennaYear - 1}, not {request.Year}.");
        }

        await EnsureTseReadyForSignedSpecialReceiptAsync(cancellationToken).ConfigureAwait(false);

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var companyProfile = await _companyProfileProvider.GetCompanyProfileAsync(cancellationToken).ConfigureAwait(false);
        var register = await _db.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == request.CashRegisterId && r.TenantId == tenantId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Cash register not found for the current tenant.");

        EnsureRegisterNotDecommissioned(register);

        var decemberMbAsJb = await GetDecemberMonatsbelegCountsAsJahresbelegAsync(tenantId, cancellationToken).ConfigureAwait(false);
        bool duplicate;
        if (decemberMbAsJb)
        {
            duplicate = await _db.PaymentDetails.AsNoTracking()
                .AnyAsync(
                    p => p.CashRegisterId == request.CashRegisterId &&
                         p.IsActive &&
                         (
                             (p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Jahresbeleg &&
                              p.RksvSpecialReceiptYear == request.Year) ||
                             (p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg &&
                              p.RksvSpecialReceiptYear == request.Year &&
                              p.RksvSpecialReceiptMonth == 12)
                         ),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            duplicate = await _db.PaymentDetails.AsNoTracking()
                .AnyAsync(
                    p => p.CashRegisterId == request.CashRegisterId &&
                         p.IsActive &&
                         p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Jahresbeleg &&
                         p.RksvSpecialReceiptYear == request.Year,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (duplicate)
        {
            throw new RksvOperationGuardException(
                RksvGuardErrorCodes.DuplicateJahresbeleg,
                $"A Jahresbeleg (or December Monatsbeleg) already exists for register {register.RegisterNumber} for year {request.Year}.");
        }

        var guest = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == WalkInCustomerConstants.GuestCustomerId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Guest customer is not available; cannot create Jahresbeleg.");

        var viennaLocalNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PostgreSqlUtcDateTime.AustriaTimeZone);
        var issuedAtUnspecified = DateTime.SpecifyKind(viennaLocalNow, DateTimeKind.Unspecified);
        var issuedAtUtc = TimeZoneInfo.ConvertTimeToUtc(issuedAtUnspecified, PostgreSqlUtcDateTime.AustriaTimeZone);
        var sequenceAnchor = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();

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
                Steuernummer = companyProfile.TaxNumber,
                CashRegisterId = request.CashRegisterId,
                Notes = BuildJahresbelegNotes(request.Reason, request.EarlyReason),
                TseSignature = sig.CompactJws,
                PrevSignatureValueUsed = sig.PrevSignatureValueUsed,
                TseTimestamp = issuedAtUtc,
                TaxDetails = JsonDocument.Parse("{}"),
                PaymentItems = JsonDocument.Parse("[]"),
                ReceiptNumber = receiptNumber,
                IsPrinted = false,
                RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Jahresbeleg,
                RksvSpecialReceiptYear = request.Year,
                RksvSpecialReceiptMonth = null,
                RksvNullbelegActsAsJahresbeleg = true,
                CreatedAt = issuedAtUtc,
                CreatedBy = actorUserId,
                IsActive = true,
            };

            var companyAddress = $"{companyProfile.Street}, {companyProfile.ZipCode} {companyProfile.City}";
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
                CompanyName = companyProfile.CompanyName,
                CompanyTaxNumber = companyProfile.TaxNumber,
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

            var receiptIdForTrackingJb = ResolveReceiptIdFromChangeTracker(paymentId);
            _db.RksvSpecialReceiptFinanzOnlineSubmissions.Add(
                _fonSubmissionTracker.CreateInitialNotRequiredRow(
                    paymentId,
                    receiptIdForTrackingJb,
                    request.CashRegisterId,
                    RksvSpecialReceiptKinds.Jahresbeleg));

            await EnqueueRksvSpecialReceiptFinanzOnlineOutboxAsync(
                FinanzOnlineRksvSpecialReceiptOutboxMessageTypes.RksvJahresbelegSubmission,
                tenantId,
                register.RegisterNumber,
                paymentId,
                receiptIdForTrackingJb,
                request.CashRegisterId,
                receiptNumber,
                RksvSpecialReceiptKinds.Jahresbeleg,
                cancellationToken).ConfigureAwait(false);

            var cashRegJahresbeleg = await _db.CashRegisters
                .FirstAsync(r => r.Id == request.CashRegisterId, cancellationToken)
                .ConfigureAwait(false);
            cashRegJahresbeleg.LastJahresbelegUtc = issuedAtUtc;
            var (viennaYearJb, viennaMonthJb) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
            if (viennaMonthJb == 12 && request.Year == viennaYearJb)
                cashRegJahresbeleg.LastMonatsbelegUtc = issuedAtUtc;

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            var receiptEntity = await _db.Receipts.AsNoTracking()
                .FirstAsync(r => r.PaymentId == paymentId, cancellationToken)
                .ConfigureAwait(false);

            var dto = await _receiptService.GetReceiptByPaymentIdAsync(paymentId).ConfigureAwait(false);
            var qr = dto?.Signature?.QrData ?? string.Empty;

            _logger.LogInformation(
                "Jahresbeleg created PaymentId={PaymentId} ReceiptNumber={ReceiptNumber} Register={Register} Year={Year}",
                paymentId, receiptNumber, register.RegisterNumber, request.Year);

            return new CreateJahresbelegResponse
            {
                PaymentId = paymentId,
                InvoiceId = invoice.Id,
                ReceiptId = receiptEntity.ReceiptId,
                ReceiptNumber = receiptNumber,
                QrData = qr,
            };
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Not for daily / monthly / yearly business closing — permanent register retirement only.
    /// </remarks>
    public async Task<CreateSchlussbelegResponse> CreateSchlussbelegAsync(
        CreateSchlussbelegRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("Actor user id is required.", nameof(actorUserId));

        await EnsureTseReadyForSignedSpecialReceiptAsync(cancellationToken).ConfigureAwait(false);

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var companyProfile = await _companyProfileProvider.GetCompanyProfileAsync(cancellationToken).ConfigureAwait(false);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await CashRegisterDatabaseLock.AcquireRegisterRowExclusiveLockAsync(_db, request.CashRegisterId, cancellationToken)
                .ConfigureAwait(false);

            var register = await _db.CashRegisters
                .FirstOrDefaultAsync(r => r.Id == request.CashRegisterId && r.TenantId == tenantId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("Cash register not found for the current tenant.");

            if (register.Status == RegisterStatus.Decommissioned)
            {
                throw new RksvOperationGuardException(
                    RksvGuardErrorCodes.RegisterAlreadyDecommissioned,
                    $"Cash register {register.RegisterNumber} is already permanently decommissioned.");
            }

            if (register.Status == RegisterStatus.Open)
            {
                throw new RksvOperationGuardException(
                    RksvGuardErrorCodes.InvalidRegisterState,
                    "Cash register has an open shift; close the register before issuing a Schlussbeleg (Endbeleg).");
            }

            if (register.Status != RegisterStatus.Closed)
            {
                throw new RksvOperationGuardException(
                    RksvGuardErrorCodes.InvalidRegisterState,
                    $"Schlussbeleg requires cash register status Closed (no open shift). Current status: {register.Status}.");
            }

            var duplicate = await _db.PaymentDetails.AsNoTracking()
                .AnyAsync(
                    p => p.CashRegisterId == request.CashRegisterId &&
                         p.IsActive &&
                         p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Schlussbeleg,
                    cancellationToken)
                .ConfigureAwait(false);
            if (duplicate)
            {
                throw new RksvOperationGuardException(
                    RksvGuardErrorCodes.DuplicateSchlussbeleg,
                    $"A Schlussbeleg already exists for register {register.RegisterNumber}.");
            }

            var guest = await _db.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == WalkInCustomerConstants.GuestCustomerId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("Guest customer is not available; cannot create Schlussbeleg.");

            var viennaLocalNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PostgreSqlUtcDateTime.AustriaTimeZone);
            var issuedAtUnspecified = DateTime.SpecifyKind(viennaLocalNow, DateTimeKind.Unspecified);
            var issuedAtUtc = TimeZoneInfo.ConvertTimeToUtc(issuedAtUnspecified, PostgreSqlUtcDateTime.AustriaTimeZone);
            var sequenceAnchor = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();

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
                Steuernummer = companyProfile.TaxNumber,
                CashRegisterId = request.CashRegisterId,
                Notes = TruncateNotes(request.Reason),
                TseSignature = sig.CompactJws,
                PrevSignatureValueUsed = sig.PrevSignatureValueUsed,
                TseTimestamp = issuedAtUtc,
                TaxDetails = JsonDocument.Parse("{}"),
                PaymentItems = JsonDocument.Parse("[]"),
                ReceiptNumber = receiptNumber,
                IsPrinted = false,
                RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Schlussbeleg,
                RksvSpecialReceiptYear = null,
                RksvSpecialReceiptMonth = null,
                RksvNullbelegActsAsJahresbeleg = false,
                CreatedAt = issuedAtUtc,
                CreatedBy = actorUserId,
                IsActive = true,
            };

            var companyAddress = $"{companyProfile.Street}, {companyProfile.ZipCode} {companyProfile.City}";
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
                CompanyName = companyProfile.CompanyName,
                CompanyTaxNumber = companyProfile.TaxNumber,
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

            var decommissionedAtUtc = DateTime.UtcNow;
            register.Status = RegisterStatus.Decommissioned;
            register.DecommissionedAtUtc = decommissionedAtUtc;
            register.DecommissionReason = TruncateNotes(request.Reason);
            register.CurrentUserId = null;
            register.CurrentUser = null;
            register.UpdatedAt = decommissionedAtUtc;

            _db.PaymentDetails.Add(payment);
            _db.Invoices.Add(invoice);
            await _receiptService.AddReceiptFromPaymentToContextAsync(payment).ConfigureAwait(false);

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            var receiptEntity = await _db.Receipts.AsNoTracking()
                .FirstAsync(r => r.PaymentId == paymentId, cancellationToken)
                .ConfigureAwait(false);

            var dto = await _receiptService.GetReceiptByPaymentIdAsync(paymentId).ConfigureAwait(false);
            var qr = dto?.Signature?.QrData ?? string.Empty;

            _logger.LogInformation(
                "Schlussbeleg created and register decommissioned PaymentId={PaymentId} ReceiptNumber={ReceiptNumber} Register={Register}",
                paymentId, receiptNumber, register.RegisterNumber);

            return new CreateSchlussbelegResponse
            {
                PaymentId = paymentId,
                InvoiceId = invoice.Id,
                ReceiptId = receiptEntity.ReceiptId,
                ReceiptNumber = receiptNumber,
                QrData = qr,
            };
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static string? TruncateCorrelation(string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            return null;
        var t = correlationId.Trim();
        return t.Length <= 100 ? t : t[..100];
    }

    private async Task EnsureTseReadyForSignedSpecialReceiptAsync(CancellationToken cancellationToken)
    {
        if (_tseOptions.IsOff)
            throw new InvalidOperationException("TSE is disabled (TseMode=Off); RKSV special receipt cannot be signed.");

        if (_tseOptions.UseSoftTseWhenNoDevice)
            return;

        var st = await _tseService.GetDeviceStatusAsync().ConfigureAwait(false);
        if (!st.IsConnected)
            throw new InvalidOperationException("TSE device is not connected; signing requires a connected TSE.");
        if (!st.IsReady)
            throw new InvalidOperationException($"TSE device is not ready (status: {st.Status}); cannot sign.");
    }
}
