using System.Text.RegularExpressions;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Tse.Fiskaly;

public interface IFiskalySetupService
{
    Task<FiskalySetupStatusDto> GetSetupStatusAsync(
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    Task<FiskalySetupOperationResult<FiskalyFonAuthDto>> AuthenticateFonAsync(
        AuthenticateFonRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<FiskalySetupOperationResult<FiskalyScuSetupDto>> InitializeScuAsync(
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<FiskalySetupOperationResult<FiskalyCashRegisterSetupDto>> InitializeCashRegisterAsync(
        Guid cashRegisterId,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);
}

public sealed class FiskalySetupOperationResult<T>
{
    public bool Success { get; init; }

    public int StatusCode { get; init; } = 200;

    public string Message { get; init; } = string.Empty;

    public T? Data { get; init; }

    public static FiskalySetupOperationResult<T> Ok(T data) =>
        new() { Success = true, StatusCode = 200, Data = data };

    public static FiskalySetupOperationResult<T> Fail(int statusCode, string message) =>
        new() { Success = false, StatusCode = statusCode, Message = message };
}

public sealed class FiskalySetupService : IFiskalySetupService
{
    public const string ScuIdSettingsKey = "Fiskaly:ScuId";
    private static readonly Regex AustrianVatId = new(@"^ATU\d{8}$", RegexOptions.CultureInvariant);

    private readonly IOptionsMonitor<FiskalyOptions> _options;
    private readonly FiskalyEnabledOverrideCache _enabledCache;
    private readonly IFiskalyClient _client;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICashRegisterManagementService _cashRegisters;
    private readonly IAuditLogService _auditLog;
    private readonly ICurrentTenantAccessor? _tenantAccessor;
    private readonly ILogger<FiskalySetupService> _logger;

    public FiskalySetupService(
        IOptionsMonitor<FiskalyOptions> options,
        FiskalyEnabledOverrideCache enabledCache,
        IFiskalyClient client,
        IDbContextFactory<AppDbContext> dbFactory,
        ICashRegisterManagementService cashRegisters,
        IAuditLogService auditLog,
        ILogger<FiskalySetupService> logger,
        ICurrentTenantAccessor? tenantAccessor = null)
    {
        _options = options;
        _enabledCache = enabledCache;
        _client = client;
        _dbFactory = dbFactory;
        _cashRegisters = cashRegisters;
        _auditLog = auditLog;
        _logger = logger;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<FiskalySetupStatusDto> GetSetupStatusAsync(
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        var enabled = opts.IsEffectivelyEnabled(_enabledCache.OverrideEnabled);
        var dto = new FiskalySetupStatusDto
        {
            Enabled = enabled,
            IsConfigured = opts.HasApiCredentials,
            Environment = opts.ResolveEnvironment()
        };

        if (!enabled || !opts.HasApiCredentials)
            return dto;

        try
        {
            var fon = await _client.GetFonAuthStatusAsync(cancellationToken).ConfigureAwait(false);
            dto.Fon = MapFon(fon);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fiskaly FON status probe failed.");
            dto.Fon.Error = "FON status could not be retrieved.";
        }

        var scuId = await ResolveExistingScuIdAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(scuId))
        {
            dto.Scu.ScuId = scuId;
            try
            {
                var scu = await _client.GetSignatureCreationUnitAsync(scuId, cancellationToken).ConfigureAwait(false);
                dto.Scu.State = scu?.State ?? "UNKNOWN";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fiskaly SCU status probe failed for {ScuId}", scuId);
                dto.Scu.State = "UNKNOWN";
            }
        }

        dto.CashRegisters = Array.Empty<FiskalyCashRegisterSetupDto>();
        if (_tenantAccessor?.TenantId is Guid tenantId)
        {
            var registers = await _cashRegisters
                .ListAsync(tenantId, excludeStatus: "Decommissioned", actorIsSuperAdmin, 1, 50, cancellationToken)
                .ConfigureAwait(false);
            var list = new List<FiskalyCashRegisterSetupDto>();
            foreach (var register in registers.Items.Take(20))
            {
                var row = new FiskalyCashRegisterSetupDto
                {
                    CashRegisterId = register.Id,
                    RegisterNumber = register.RegisterNumber,
                    Location = register.Location,
                    State = "UNKNOWN"
                };
                try
                {
                    var remote = await _client.GetCashRegisterAsync(register.Id, cancellationToken).ConfigureAwait(false);
                    row.State = remote?.State ?? "MISSING";
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Fiskaly cash register status probe failed for {CashRegisterId}", register.Id);
                }

                list.Add(row);
            }

            dto.CashRegisters = list;
        }

        return dto;
    }

    public async Task<FiskalySetupOperationResult<FiskalyFonAuthDto>> AuthenticateFonAsync(
        AuthenticateFonRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var disabled = DisabledResult<FiskalyFonAuthDto>();
        if (disabled is not null)
            return disabled;

        var result = await _client
            .AuthenticateFonAsync(
                new FiskalyFonAuthRequest(
                    request.FonParticipantId.Trim(),
                    request.FonUserId.Trim(),
                    request.FonUserPin),
                cancellationToken)
            .ConfigureAwait(false);

        var dto = MapFon(result);
        if (!result.IsAuthenticated)
        {
            return FiskalySetupOperationResult<FiskalyFonAuthDto>.Fail(
                400,
                string.IsNullOrWhiteSpace(result.Error)
                    ? "FON authentication failed."
                    : result.Error);
        }

        await _auditLog.LogSystemOperationAsync(
                action: "FISKALY_FON_AUTHENTICATED",
                entityType: "FiskalyFonAuth",
                userId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
                userRole: "SuperAdmin",
                description: "Fiskaly FON authentication succeeded.",
                status: AuditLogStatus.Success,
                actionType: AuditEventType.FiskalyFonAuthenticated,
                tenantId: _tenantAccessor?.TenantId,
                newValues: new
                {
                    ParticipantId = dto.ParticipantId,
                    UserId = dto.UserId,
                    Status = dto.AuthenticationStatus
                })
            .ConfigureAwait(false);

        return FiskalySetupOperationResult<FiskalyFonAuthDto>.Ok(dto);
    }

    public async Task<FiskalySetupOperationResult<FiskalyScuSetupDto>> InitializeScuAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var disabled = DisabledResult<FiskalyScuSetupDto>();
        if (disabled is not null)
            return disabled;

        var fonGate = await RequireFonAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        if (fonGate is not null)
            return FiskalySetupOperationResult<FiskalyScuSetupDto>.Fail(fonGate.StatusCode, fonGate.Message);

        var scuId = await ResolveOrCreateScuIdAsync(cancellationToken).ConfigureAwait(false);
        var existing = await _client.GetSignatureCreationUnitAsync(scuId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            var vatId = await ResolveVatIdAsync(cancellationToken).ConfigureAwait(false);
            existing = await _client
                .CreateSignatureCreationUnitAsync(Guid.Parse(scuId), vatId, "Regkasse", cancellationToken)
                .ConfigureAwait(false);
        }

        FiskalyScuInfo scu = existing;
        if (!IsState(scu.State, FiskalyResourceStates.Initialized))
        {
            scu = await _client
                .UpdateSignatureCreationUnitStateAsync(scu.Id, FiskalyResourceStates.Initialized, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!IsState(scu.State, FiskalyResourceStates.Initialized))
        {
            return FiskalySetupOperationResult<FiskalyScuSetupDto>.Fail(
                400,
                $"SCU initialization failed (current state: {scu.State}).");
        }

        await PersistScuIdAsync(scu.Id, actorUserId, cancellationToken).ConfigureAwait(false);
        await TouchTseDeviceAsync(scu.Id, cashRegisterId: null, cancellationToken).ConfigureAwait(false);

        await _auditLog.LogSystemOperationAsync(
                action: "FISKALY_SCU_INITIALIZED",
                entityType: "FiskalyScu",
                userId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
                userRole: "SuperAdmin",
                description: $"Fiskaly SCU {scu.Id} set to INITIALIZED.",
                status: AuditLogStatus.Success,
                actionType: AuditEventType.FiskalyScuInitialized,
                tenantId: _tenantAccessor?.TenantId,
                newValues: new { ScuId = scu.Id, State = scu.State })
            .ConfigureAwait(false);

        return FiskalySetupOperationResult<FiskalyScuSetupDto>.Ok(new FiskalyScuSetupDto
        {
            ScuId = scu.Id,
            State = scu.State,
            InitializedAt = DateTimeOffset.UtcNow
        });
    }

    public async Task<FiskalySetupOperationResult<FiskalyCashRegisterSetupDto>> InitializeCashRegisterAsync(
        Guid cashRegisterId,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        var disabled = DisabledResult<FiskalyCashRegisterSetupDto>();
        if (disabled is not null)
            return disabled;

        var fonGate = await RequireFonAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        if (fonGate is not null)
            return FiskalySetupOperationResult<FiskalyCashRegisterSetupDto>.Fail(fonGate.StatusCode, fonGate.Message);

        var scuId = await ResolveExistingScuIdAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(scuId))
        {
            return FiskalySetupOperationResult<FiskalyCashRegisterSetupDto>.Fail(
                400,
                "SCU ID not found. Initialize the SCU first.");
        }

        var scu = await _client.GetSignatureCreationUnitAsync(scuId, cancellationToken).ConfigureAwait(false);
        if (scu is null || !IsState(scu.State, FiskalyResourceStates.Initialized))
        {
            return FiskalySetupOperationResult<FiskalyCashRegisterSetupDto>.Fail(
                400,
                "SCU must be initialized first.");
        }

        var register = await _cashRegisters
            .GetByIdAsync(cashRegisterId, _tenantAccessor?.TenantId, actorIsSuperAdmin, cancellationToken)
            .ConfigureAwait(false);
        if (register is null)
            return FiskalySetupOperationResult<FiskalyCashRegisterSetupDto>.Fail(404, "Cash register not found.");

        if (register.Status == RegisterStatus.Decommissioned)
        {
            return FiskalySetupOperationResult<FiskalyCashRegisterSetupDto>.Fail(
                400,
                "Decommissioned cash registers cannot be initialized.");
        }

        var remote = await _client.GetCashRegisterAsync(register.Id, cancellationToken).ConfigureAwait(false)
            ?? await _client
                .CreateCashRegisterAsync(
                    register.Id,
                    string.IsNullOrWhiteSpace(register.Location) ? "Regkasse POS" : register.Location,
                    cancellationToken)
                .ConfigureAwait(false);

        if (IsState(remote.State, FiskalyResourceStates.Created))
        {
            remote = await _client
                .UpdateCashRegisterStateAsync(register.Id, FiskalyResourceStates.Registered, cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsState(remote.State, FiskalyResourceStates.Registered))
        {
            remote = await _client
                .UpdateCashRegisterStateAsync(register.Id, FiskalyResourceStates.Initialized, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!IsState(remote.State, FiskalyResourceStates.Initialized))
        {
            return FiskalySetupOperationResult<FiskalyCashRegisterSetupDto>.Fail(
                400,
                $"Cash register initialization failed (current state: {remote.State}).");
        }

        await TouchTseDeviceAsync(scuId, register.Id, cancellationToken).ConfigureAwait(false);

        await _auditLog.LogSystemOperationAsync(
                action: "FISKALY_CASH_REGISTER_INITIALIZED",
                entityType: "CashRegister",
                userId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
                userRole: "SuperAdmin",
                description: $"Fiskaly cash register {register.Id} set to INITIALIZED.",
                status: AuditLogStatus.Success,
                actionType: AuditEventType.FiskalyCashRegisterInitialized,
                entityId: register.Id,
                tenantId: register.TenantId,
                newValues: new { CashRegisterId = register.Id, State = remote.State })
            .ConfigureAwait(false);

        return FiskalySetupOperationResult<FiskalyCashRegisterSetupDto>.Ok(new FiskalyCashRegisterSetupDto
        {
            CashRegisterId = register.Id,
            RegisterNumber = register.RegisterNumber,
            Location = register.Location,
            State = remote.State,
            InitializedAt = DateTimeOffset.UtcNow
        });
    }

    private FiskalySetupOperationResult<T>? DisabledResult<T>()
    {
        var opts = _options.CurrentValue;
        if (!opts.IsEffectivelyEnabled(_enabledCache.OverrideEnabled))
            return FiskalySetupOperationResult<T>.Fail(400, "Fiskaly is disabled.");
        if (!opts.HasApiCredentials)
            return FiskalySetupOperationResult<T>.Fail(400, "Fiskaly API credentials are not configured.");
        return null;
    }

    private async Task<FiskalySetupOperationResult<object>?> RequireFonAuthenticatedAsync(
        CancellationToken cancellationToken)
    {
        var fon = await _client.GetFonAuthStatusAsync(cancellationToken).ConfigureAwait(false);
        if (fon.IsAuthenticated)
            return null;

        return FiskalySetupOperationResult<object>.Fail(400, "FON authentication required first.");
    }

    private async Task<string?> ResolveExistingScuIdAsync(CancellationToken cancellationToken)
    {
        var configured = _options.CurrentValue.SignatureCreationUnitId;
        if (Guid.TryParse(configured, out var configuredId) && configuredId != Guid.Empty)
            return configuredId.ToString("D");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var tenantId = _tenantAccessor?.TenantId;
        var stored = await db.TenantSettings.AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Key == ScuIdSettingsKey && s.TenantId == tenantId,
                cancellationToken)
            .ConfigureAwait(false);
        if (Guid.TryParse(stored?.Value, out var storedId) && storedId != Guid.Empty)
            return storedId.ToString("D");

        var device = await db.TseDevices.AsNoTracking()
            .Where(d => d.IsActive && (d.DeviceType == "fiskaly" || d.Provider == "fiskaly"))
            .Where(d => tenantId == null || d.TenantId == tenantId)
            .Select(d => d.DeviceId ?? d.SerialNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return Guid.TryParse(device, out var deviceId) && deviceId != Guid.Empty
            ? deviceId.ToString("D")
            : null;
    }

    private async Task<string> ResolveOrCreateScuIdAsync(CancellationToken cancellationToken)
    {
        var existing = await ResolveExistingScuIdAsync(cancellationToken).ConfigureAwait(false);
        return existing ?? Guid.NewGuid().ToString("D");
    }

    private async Task PersistScuIdAsync(string scuId, string actorUserId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(scuId, out _))
            return;

        var tenantId = _tenantAccessor?.TenantId;
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.TenantSettings
            .FirstOrDefaultAsync(s => s.Key == ScuIdSettingsKey && s.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            db.TenantSettings.Add(new TenantSetting
            {
                TenantId = tenantId,
                Key = ScuIdSettingsKey,
                Value = scuId,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByUserId = actorUserId
            });
        }
        else
        {
            row.Value = scuId;
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedByUserId = actorUserId;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task TouchTseDeviceAsync(string scuId, Guid? cashRegisterId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var tenantId = _tenantAccessor?.TenantId;
        var query = db.TseDevices.Where(d => d.IsActive && (d.DeviceType == "fiskaly" || d.Provider == "fiskaly"));
        if (tenantId is Guid tid)
            query = query.Where(d => d.TenantId == tid);
        if (cashRegisterId is Guid crId)
            query = query.Where(d => d.CashRegisterId == crId || d.KassenId == crId);

        var device = await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (device is null)
            return;

        device.DeviceId = scuId;
        device.IsConnected = true;
        device.HealthMessage = FiskalyResourceStates.Initialized;
        device.LastConnectionTime = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ResolveVatIdAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var fromSettings = await db.CompanySettings.AsNoTracking()
            .Select(s => s.CompanyTaxNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(fromSettings)
            && AustrianVatId.IsMatch(fromSettings.Trim().ToUpperInvariant()))
        {
            return fromSettings.Trim().ToUpperInvariant();
        }

        return FiskalyConnectionProbe.DefaultTestVatId;
    }

    private static FiskalyFonAuthDto MapFon(FiskalyFonAuthResult result) => new()
    {
        Authenticated = result.IsAuthenticated,
        AuthenticationStatus = result.AuthenticationStatus,
        ParticipantId = result.ParticipantId,
        UserId = result.UserId,
        AuthenticatedAt = result.AuthenticatedAt,
        Error = result.Error
    };

    private static bool IsState(string? actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
}
