using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Tenancy;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Tse.Fiskaly;

public interface IFiskalySignTestService
{
    IReadOnlyList<FiskalySignTestScenarioDto> GetScenarios();

    Task<FiskalySetupOperationResult<FiskalySignTestResultDto>> SignAsync(
        FiskalySignTestRequest request,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    Task<FiskalySetupOperationResult<FiskalyVerifyTestResultDto>> VerifyAsync(
        FiskalyVerifyTestRequest request,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Development-only Super Admin helper: signs a synthetic fiskaly receipt (not a POS payment).
/// </summary>
public sealed class FiskalySignTestService : IFiskalySignTestService
{
    private readonly IOptionsMonitor<FiskalyOptions> _options;
    private readonly FiskalyEnabledOverrideCache _enabledCache;
    private readonly IFiskalyClient _client;
    private readonly ICashRegisterManagementService _cashRegisters;
    private readonly IAuditLogService _auditLog;
    private readonly ICurrentTenantAccessor? _tenantAccessor;
    private readonly ILogger<FiskalySignTestService> _logger;

    public FiskalySignTestService(
        IOptionsMonitor<FiskalyOptions> options,
        FiskalyEnabledOverrideCache enabledCache,
        IFiskalyClient client,
        ICashRegisterManagementService cashRegisters,
        IAuditLogService auditLog,
        ILogger<FiskalySignTestService> logger,
        ICurrentTenantAccessor? tenantAccessor = null)
    {
        _options = options;
        _enabledCache = enabledCache;
        _client = client;
        _cashRegisters = cashRegisters;
        _auditLog = auditLog;
        _logger = logger;
        _tenantAccessor = tenantAccessor;
    }

    public IReadOnlyList<FiskalySignTestScenarioDto> GetScenarios() => FiskalySignTestScenarios.All;

    public async Task<FiskalySetupOperationResult<FiskalySignTestResultDto>> SignAsync(
        FiskalySignTestRequest request,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var gate = await GateAsync(request.CashRegisterId, actorIsSuperAdmin, cancellationToken)
            .ConfigureAwait(false);
        if (gate.Error is not null)
            return gate.Error;

        var scenario = FiskalySignTestScenarios.Find(request.Scenario);
        if (scenario is null)
            return Fail<FiskalySignTestResultDto>(400, "Unknown signing scenario.");
        if (string.Equals(scenario.Id, FiskalySignTestScenarioIds.Tagesabschluss, StringComparison.OrdinalIgnoreCase))
            return Fail<FiskalySignTestResultDto>(
                400,
                "Tagesabschluss must be submitted from the existing TSE-signed DailyClosing via FiskalyReceiptService.");
        if (string.Equals(scenario.Id, FiskalySignTestScenarioIds.MonthlyClose, StringComparison.OrdinalIgnoreCase)
            || string.Equals(scenario.Id, FiskalySignTestScenarioIds.YearlyClose, StringComparison.OrdinalIgnoreCase))
            return Fail<FiskalySignTestResultDto>(
                400,
                "Monatsbeleg and Jahresbeleg must be created via FiskalyReceiptService (RKSV special receipts).",
                FiskalyReceiptErrorCodes.ValidationError);
        if (!scenario.CanSign)
            return Fail<FiskalySignTestResultDto>(400, scenario.Description);

        var receiptId = Guid.NewGuid();
        var data = ApplyAmountOverride(
            FiskalySignTestScenarios.ToTransactionData(scenario, request.CashRegisterId),
            request,
            scenario);

        _logger.LogInformation(
            "Signing fiskaly test receipt. Scenario={Scenario} Register={CashRegisterId} Receipt={ReceiptId}",
            scenario.Id,
            request.CashRegisterId,
            receiptId);

        FiskalySignedReceipt signed;
        try
        {
            signed = await _client
                .SignReceiptAsync(request.CashRegisterId, receiptId, data, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (FiskalyApiException ex)
        {
            _logger.LogWarning(ex, "Fiskaly test signing failed for register {CashRegisterId}", request.CashRegisterId);
            var mapped = FiskalyReceiptErrorMapper.FromException(ex);
            return Fail<FiskalySignTestResultDto>(400, mapped.Message, mapped.Code, mapped.Details);
        }

        var qr = FiskalyQrCodeValidator.Validate(signed.QrCodeData);
        var dto = new FiskalySignTestResultDto
        {
            Success = true,
            Scenario = scenario.Id,
            ReceiptId = signed.Id,
            ReceiptNumber = signed.ReceiptNumber,
            QrCodeData = signed.QrCodeData,
            TimeSignature = signed.TimeSignature,
            Signed = signed.Signed,
            Hints = signed.Hints,
            CashRegisterSerial = signed.CashRegisterSerialNumber,
            ReceiptType = signed.ReceiptType ?? scenario.ReceiptType,
            Environment = signed.Environment,
            FonValidationsJson = signed.FonValidationsJson,
            QrValidation = qr,
            Checks = BuildChecks(signed, qr)
        };

        await _auditLog.LogSystemOperationAsync(
                action: "FISKALY_TEST_RECEIPT_SIGNED",
                entityType: "FiskalyReceipt",
                userId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
                userRole: Roles.SuperAdmin,
                description: $"Development fiskaly test receipt signed ({scenario.Id}).",
                status: AuditLogStatus.Success,
                actionType: AuditEventType.FiskalyTestReceiptSigned,
                tenantId: _tenantAccessor?.TenantId,
                newValues: new
                {
                    signed.Id,
                    signed.ReceiptNumber,
                    Scenario = scenario.Id,
                    CashRegisterId = request.CashRegisterId
                })
            .ConfigureAwait(false);

        return FiskalySetupOperationResult<FiskalySignTestResultDto>.Ok(dto);
    }

    public async Task<FiskalySetupOperationResult<FiskalyVerifyTestResultDto>> VerifyAsync(
        FiskalyVerifyTestRequest request,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ReceiptId))
            return Fail<FiskalyVerifyTestResultDto>(400, "Receipt id or number is required.");

        var gate = await GateAsync(request.CashRegisterId, actorIsSuperAdmin, cancellationToken)
            .ConfigureAwait(false);
        if (gate.Error is not null)
            return Fail<FiskalyVerifyTestResultDto>(
                gate.Error.StatusCode,
                gate.Error.Message,
                gate.Error.Code,
                gate.Error.Details);

        FiskalySignedReceipt receipt;
        try
        {
            receipt = await _client
                .GetReceiptAsync(request.CashRegisterId, request.ReceiptId.Trim(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (FiskalyApiException ex)
        {
            _logger.LogWarning(ex, "Fiskaly test verify failed for register {CashRegisterId}", request.CashRegisterId);
            var mapped = FiskalyReceiptErrorMapper.FromException(ex);
            return Fail<FiskalyVerifyTestResultDto>(400, mapped.Message, mapped.Code, mapped.Details);
        }

        var qr = FiskalyQrCodeValidator.Validate(receipt.QrCodeData);
        return FiskalySetupOperationResult<FiskalyVerifyTestResultDto>.Ok(new FiskalyVerifyTestResultDto
        {
            ReceiptId = receipt.Id,
            ReceiptNumber = receipt.ReceiptNumber,
            QrCodeData = receipt.QrCodeData,
            TimeSignature = receipt.TimeSignature,
            Signed = receipt.Signed,
            Hints = receipt.Hints,
            CashRegisterSerial = receipt.CashRegisterSerialNumber,
            ReceiptType = receipt.ReceiptType,
            Environment = receipt.Environment,
            FonValidationsJson = receipt.FonValidationsJson,
            QrValidation = qr,
            Checks = BuildChecks(receipt, qr)
        });
    }

    private async Task<(FiskalySetupOperationResult<FiskalySignTestResultDto>? Error, Guid CashRegisterId)> GateAsync(
        Guid cashRegisterId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        if (!opts.IsEffectivelyEnabled(_enabledCache.OverrideEnabled))
            return (Fail<FiskalySignTestResultDto>(400, "Fiskaly is disabled.", FiskalyReceiptErrorCodes.FiskalyDisabled), cashRegisterId);

        if (!opts.HasApiCredentials)
            return (Fail<FiskalySignTestResultDto>(400, "Fiskaly API credentials are not configured.", FiskalyReceiptErrorCodes.FiskalyNotConfigured), cashRegisterId);

        if (string.Equals(opts.ResolveEnvironment(), FiskalyOptions.LiveEnvironment, StringComparison.OrdinalIgnoreCase))
        {
            return (Fail<FiskalySignTestResultDto>(
                400,
                "Test signing is not allowed against LIVE fiskaly.",
                FiskalyReceiptErrorCodes.FiskalyLiveBlocked), cashRegisterId);
        }

        if (cashRegisterId == Guid.Empty)
            return (Fail<FiskalySignTestResultDto>(400, "Cash register id is required.", FiskalyReceiptErrorCodes.CashRegisterIdRequired), cashRegisterId);

        var register = await _cashRegisters
            .GetByIdAsync(cashRegisterId, _tenantAccessor?.TenantId, actorIsSuperAdmin, cancellationToken)
            .ConfigureAwait(false);
        if (register is null)
            return (Fail<FiskalySignTestResultDto>(404, "Cash register not found.", FiskalyReceiptErrorCodes.CashRegisterNotFound), cashRegisterId);

        FiskalyCashRegisterInfo? remote;
        try
        {
            remote = await _client.GetCashRegisterAsync(cashRegisterId, cancellationToken).ConfigureAwait(false);
        }
        catch (FiskalyApiException ex)
        {
            var mapped = FiskalyReceiptErrorMapper.FromException(ex);
            return (Fail<FiskalySignTestResultDto>(400, mapped.Message, mapped.Code, mapped.Details), cashRegisterId);
        }

        if (remote is null)
            return (Fail<FiskalySignTestResultDto>(400, "Cash register is not registered at fiskaly.", FiskalyReceiptErrorCodes.FiskalyRegisterNotFound), cashRegisterId);

        if (!string.Equals(remote.State, FiskalyResourceStates.Initialized, StringComparison.OrdinalIgnoreCase))
        {
            return (Fail<FiskalySignTestResultDto>(
                400,
                $"Cash register is not INITIALIZED (current state: {remote.State}).",
                FiskalyReceiptErrorCodes.FiskalyRegisterNotInitialized), cashRegisterId);
        }

        return (null, cashRegisterId);
    }

    private static FiskalyReceiptChecksDto BuildChecks(FiskalySignedReceipt receipt, FiskalyQrValidationDto qr)
    {
        var hasNumber = !string.IsNullOrWhiteSpace(receipt.ReceiptNumber);
        var sequential = hasNumber
            && long.TryParse(receipt.ReceiptNumber, out var n)
            && n > 0;

        return new FiskalyReceiptChecksDto
        {
            QrFormatValid = qr.IsValid,
            HasReceiptNumber = hasNumber,
            ReceiptNumberLooksSequential = sequential,
            HasTimeSignature = receipt.TimeSignature is > 0,
            HasCashRegisterSerial = !string.IsNullOrWhiteSpace(receipt.CashRegisterSerialNumber),
            Signed = receipt.Signed
        };
    }

    private static FiskalyTransactionData ApplyAmountOverride(
        FiskalyTransactionData data,
        FiskalySignTestRequest request,
        FiskalySignTestScenarioDto scenario)
    {
        if (request.Amount is null)
            return data;

        var amount = request.Amount.Value;
        if (string.Equals(scenario.ReceiptType, "CANCELLATION", StringComparison.OrdinalIgnoreCase) && amount > 0)
            amount = -amount;

        var vat = string.IsNullOrWhiteSpace(request.VatRate)
            ? data.VatRate
            : request.VatRate.Trim().ToUpperInvariant();

        return new FiskalyTransactionData
        {
            CashRegisterId = data.CashRegisterId,
            ReceiptType = data.ReceiptType,
            PaymentType = data.PaymentType,
            CurrencyCode = data.CurrencyCode,
            SchemaKind = data.SchemaKind,
            TotalAmount = amount,
            VatRate = vat,
            AmountsPerVatRate = [new FiskalyVatAmount { VatRate = vat, Amount = amount }],
            LineItems =
            [
                new FiskalyLineItem
                {
                    Quantity = "1",
                    Text = "Test Produkt",
                    PricePerUnit = FiskalyReceiptSchemaMapper.FormatAmount(amount)
                }
            ]
        };
    }

    private static FiskalySetupOperationResult<T> Fail<T>(
        int statusCode,
        string message,
        string? code = null,
        string? details = null) =>
        FiskalySetupOperationResult<T>.Fail(statusCode, message, code, details);
}
