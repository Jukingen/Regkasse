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
        if (!scenario.CanSign)
            return Fail<FiskalySignTestResultDto>(400, scenario.Description);

        var receiptId = Guid.NewGuid();
        var data = FiskalySignTestScenarios.ToTransactionData(scenario, request.CashRegisterId);

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
            return Fail<FiskalySignTestResultDto>(400, ex.Message);
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
            return Fail<FiskalyVerifyTestResultDto>(gate.Error.StatusCode, gate.Error.Message);

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
            return Fail<FiskalyVerifyTestResultDto>(400, ex.Message);
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
            return (Fail<FiskalySignTestResultDto>(400, "Fiskaly is disabled."), cashRegisterId);

        if (!opts.HasApiCredentials)
            return (Fail<FiskalySignTestResultDto>(400, "Fiskaly API credentials are not configured."), cashRegisterId);

        if (string.Equals(opts.ResolveEnvironment(), FiskalyOptions.LiveEnvironment, StringComparison.OrdinalIgnoreCase))
        {
            return (Fail<FiskalySignTestResultDto>(
                400,
                "Test signing is not allowed against LIVE fiskaly."), cashRegisterId);
        }

        if (cashRegisterId == Guid.Empty)
            return (Fail<FiskalySignTestResultDto>(400, "Cash register id is required."), cashRegisterId);

        var register = await _cashRegisters
            .GetByIdAsync(cashRegisterId, _tenantAccessor?.TenantId, actorIsSuperAdmin, cancellationToken)
            .ConfigureAwait(false);
        if (register is null)
            return (Fail<FiskalySignTestResultDto>(404, "Cash register not found."), cashRegisterId);

        FiskalyCashRegisterInfo? remote;
        try
        {
            remote = await _client.GetCashRegisterAsync(cashRegisterId, cancellationToken).ConfigureAwait(false);
        }
        catch (FiskalyApiException ex)
        {
            return (Fail<FiskalySignTestResultDto>(400, ex.Message), cashRegisterId);
        }

        if (remote is null)
            return (Fail<FiskalySignTestResultDto>(400, "Cash register is not registered at fiskaly."), cashRegisterId);

        if (!string.Equals(remote.State, FiskalyResourceStates.Initialized, StringComparison.OrdinalIgnoreCase))
        {
            return (Fail<FiskalySignTestResultDto>(
                400,
                $"Cash register is not INITIALIZED (current state: {remote.State})."), cashRegisterId);
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

    private static FiskalySetupOperationResult<T> Fail<T>(int statusCode, string message) =>
        FiskalySetupOperationResult<T>.Fail(statusCode, message);
}
