using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tse;

public sealed class TseSimulatorService : ITseSimulatorService
{
    private readonly AppDbContext _db;
    private readonly ITseSimulatorStateStore _state;
    private readonly ITseDeviceHealthCheckService _healthCheck;
    private readonly IAuditLogService _auditLog;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<TseSimulatorService> _logger;

    public TseSimulatorService(
        AppDbContext db,
        ITseSimulatorStateStore state,
        ITseDeviceHealthCheckService healthCheck,
        IAuditLogService auditLog,
        IHostEnvironment environment,
        ILogger<TseSimulatorService> logger)
    {
        _db = db;
        _state = state;
        _healthCheck = healthCheck;
        _auditLog = auditLog;
        _environment = environment;
        _logger = logger;
    }

    public Task<IReadOnlyList<TseSimulationScenarioDto>> GetAvailableScenariosAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsSimulatorEnabled())
            return Task.FromResult<IReadOnlyList<TseSimulationScenarioDto>>(Array.Empty<TseSimulationScenarioDto>());

        IReadOnlyList<TseSimulationScenarioDto> scenarios =
        [
            Scenario("failure.NetworkTimeout", "Failure", TseSimulatorFailureType.NetworkTimeout,
                "Network timeout", "Marks device disconnected with a timeout error (probe fails)."),
            Scenario("failure.ConnectionLost", "Failure", TseSimulatorFailureType.ConnectionLost,
                "Connection lost", "Marks device disconnected / not ready for invoices."),
            Scenario("failure.CertificateInvalid", "Failure", TseSimulatorFailureType.CertificateInvalid,
                "Certificate invalid", "Sets certificate status to REVOKED."),
            Scenario("failure.SignatureError", "Failure", TseSimulatorFailureType.SignatureError,
                "Signature error", "Sets device error flag (does not rewrite fiscal signatures)."),
            Scenario("failure.RateLimitExceeded", "Failure", TseSimulatorFailureType.RateLimitExceeded,
                "Rate limit exceeded", "Blocks invoice creation with rate-limit error message."),
            Scenario("failure.InternalServerError", "Failure", TseSimulatorFailureType.InternalServerError,
                "Internal server error", "Marks device unhealthy with internal error message."),
            new TseSimulationScenarioDto
            {
                Id = "latency",
                Category = "Latency",
                Name = "Network latency",
                Description = "Adds artificial delay to the next health probes (in-memory, Development only).",
            },
            new TseSimulationScenarioDto
            {
                Id = "certificate.expiry",
                Category = "Certificate",
                Name = "Certificate expiry",
                Description = "Sets ExpiresAt (and EXPIRED when in the past).",
            },
            new TseSimulationScenarioDto
            {
                Id = "reset",
                Category = "Reset",
                Name = "Reset simulation",
                Description = "Restores baseline device health fields and clears latency overlay.",
            },
        ];

        return Task.FromResult(scenarios);
    }

    public async Task<TseSimulationResultDto> SimulateTseFailureAsync(
        Guid deviceId,
        TseSimulatorFailureType type,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsSimulatorEnabled())
            return Denied(deviceId);

        var device = await LoadDeviceAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (device is null)
            return NotFound(deviceId);

        EnsureBaseline(device);
        ApplyFailure(device, type);
        device.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var scenarioId = $"failure.{type}";
        _state.SetActiveScenario(deviceId, scenarioId);

        await RefreshHealthAsync(deviceId, cancellationToken).ConfigureAwait(false);
        await AuditAsync(
                "TSE_SIMULATION_FAILURE",
                actorUserId,
                device,
                new { FailureType = type.ToString(), ScenarioId = scenarioId },
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogWarning(
            "TSE simulation failure applied DeviceId={DeviceId} Type={Type} Actor={Actor}",
            deviceId,
            type,
            actorUserId);

        return await OkAsync(deviceId, scenarioId, $"Applied failure scenario {type}.", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TseSimulationResultDto> SimulateNetworkLatencyAsync(
        Guid deviceId,
        int latencyMs,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsSimulatorEnabled())
            return Denied(deviceId);

        var device = await LoadDeviceAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (device is null)
            return NotFound(deviceId);

        EnsureBaseline(device);
        var clamped = Math.Clamp(latencyMs, 0, 60_000);
        _state.SetLatencyMs(deviceId, clamped);
        _state.SetActiveScenario(deviceId, clamped > 0 ? "latency" : null);

        await AuditAsync(
                "TSE_SIMULATION_LATENCY",
                actorUserId,
                device,
                new { LatencyMs = clamped },
                cancellationToken)
            .ConfigureAwait(false);

        return await OkAsync(
                deviceId,
                clamped > 0 ? "latency" : "reset",
                clamped > 0
                    ? $"Health probes will delay ~{clamped} ms for this device."
                    : "Latency overlay cleared.",
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TseSimulationResultDto> SimulateCertificateExpiryAsync(
        Guid deviceId,
        DateTime expiryDateUtc,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsSimulatorEnabled())
            return Denied(deviceId);

        var device = await LoadDeviceAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (device is null)
            return NotFound(deviceId);

        EnsureBaseline(device);

        var expiry = NormalizeUtc(expiryDateUtc);
        device.ExpiresAt = expiry;
        device.CertificateStatus = expiry <= DateTime.UtcNow ? "EXPIRED" : "VALID";
        device.ErrorMessage = expiry <= DateTime.UtcNow
            ? "SIM: certificate expired"
            : $"SIM: certificate expires at {expiry:u}";
        device.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _state.SetActiveScenario(deviceId, "certificate.expiry");
        await RefreshHealthAsync(deviceId, cancellationToken).ConfigureAwait(false);
        await AuditAsync(
                "TSE_SIMULATION_CERT_EXPIRY",
                actorUserId,
                device,
                new { ExpiryDateUtc = expiry },
                cancellationToken)
            .ConfigureAwait(false);

        return await OkAsync(
                deviceId,
                "certificate.expiry",
                $"Certificate ExpiresAt set to {expiry:u}.",
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TseSimulationResultDto> ResetSimulationAsync(
        Guid deviceId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsSimulatorEnabled())
            return Denied(deviceId);

        var device = await LoadDeviceAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (device is null)
            return NotFound(deviceId);

        if (_state.TryGetBaseline(deviceId, out var baseline))
        {
            device.IsConnected = baseline.IsConnected;
            device.CanCreateInvoices = baseline.CanCreateInvoices;
            device.CertificateStatus = baseline.CertificateStatus;
            device.MemoryStatus = baseline.MemoryStatus;
            device.ErrorMessage = baseline.ErrorMessage;
            device.ExpiresAt = baseline.ExpiresAt;
            device.IssuedAt = baseline.IssuedAt;
        }
        else
        {
            // Default healthy soft baseline when no snapshot was captured.
            device.IsConnected = true;
            device.CanCreateInvoices = true;
            device.CertificateStatus = "VALID";
            device.MemoryStatus = "OK";
            device.ErrorMessage = null;
            if (device.ExpiresAt is null || device.ExpiresAt <= DateTime.UtcNow)
                device.ExpiresAt = DateTime.UtcNow.AddYears(2);
        }

        device.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _state.Clear(deviceId);
        await RefreshHealthAsync(deviceId, cancellationToken).ConfigureAwait(false);
        await AuditAsync("TSE_SIMULATION_RESET", actorUserId, device, new { }, cancellationToken)
            .ConfigureAwait(false);

        return await OkAsync(deviceId, "reset", "Simulation state reset.", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TseSimulationDeviceSnapshotDto?> GetSnapshotAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsSimulatorEnabled() || deviceId == Guid.Empty)
            return null;

        var device = await _db.TseDevices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
            .ConfigureAwait(false);
        return device is null ? null : ToSnapshot(device);
    }

    private bool IsSimulatorEnabled() => _environment.IsDevelopment();

    private void EnsureBaseline(TseDevice device)
    {
        if (_state.TryGetBaseline(device.Id, out _))
            return;

        _state.SaveBaseline(device.Id, new TseSimulatorBaseline
        {
            IsConnected = device.IsConnected,
            CanCreateInvoices = device.CanCreateInvoices,
            CertificateStatus = device.CertificateStatus,
            MemoryStatus = device.MemoryStatus,
            ErrorMessage = device.ErrorMessage,
            ExpiresAt = device.ExpiresAt,
            IssuedAt = device.IssuedAt,
        });
    }

    private static void ApplyFailure(TseDevice device, TseSimulatorFailureType type)
    {
        switch (type)
        {
            case TseSimulatorFailureType.NetworkTimeout:
                device.IsConnected = false;
                device.CanCreateInvoices = false;
                device.ErrorMessage = "SIM: network timeout";
                break;
            case TseSimulatorFailureType.ConnectionLost:
                device.IsConnected = false;
                device.CanCreateInvoices = false;
                device.ErrorMessage = "SIM: connection lost";
                break;
            case TseSimulatorFailureType.CertificateInvalid:
                device.CertificateStatus = "REVOKED";
                device.CanCreateInvoices = false;
                device.ErrorMessage = "SIM: certificate invalid/revoked";
                break;
            case TseSimulatorFailureType.SignatureError:
                device.IsConnected = true;
                device.CanCreateInvoices = false;
                device.ErrorMessage = "SIM: signature error";
                break;
            case TseSimulatorFailureType.RateLimitExceeded:
                device.IsConnected = true;
                device.CanCreateInvoices = false;
                device.ErrorMessage = "SIM: rate limit exceeded";
                break;
            case TseSimulatorFailureType.InternalServerError:
                device.IsConnected = false;
                device.CanCreateInvoices = false;
                device.ErrorMessage = "SIM: internal server error";
                break;
            default:
                device.ErrorMessage = $"SIM: unknown failure {type}";
                break;
        }
    }

    private async Task RefreshHealthAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        try
        {
            await _healthCheck.CheckHealthAsync(deviceId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Post-simulation health refresh failed for device {DeviceId}", deviceId);
        }
    }

    private async Task AuditAsync(
        string action,
        string? actorUserId,
        TseDevice device,
        object responseData,
        CancellationToken cancellationToken)
    {
        try
        {
            await _auditLog.LogSystemOperationAsync(
                action,
                "TseDevice",
                userId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId.Trim(),
                userRole: "SuperAdmin",
                description: $"TSE Development simulator: {action}",
                status: AuditLogStatus.Success,
                responseData: responseData,
                tenantId: device.TenantId,
                entityId: device.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit failed for TSE simulator action {Action}", action);
        }

        _ = cancellationToken;
    }

    private async Task<TseDevice?> LoadDeviceAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        if (deviceId == Guid.Empty)
            return null;
        return await _db.TseDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TseSimulationResultDto> OkAsync(
        Guid deviceId,
        string scenarioId,
        string message,
        CancellationToken cancellationToken)
    {
        var snap = await GetSnapshotAsync(deviceId, cancellationToken).ConfigureAwait(false);
        return new TseSimulationResultDto
        {
            Success = true,
            DeviceId = deviceId,
            ScenarioId = scenarioId,
            Message = message,
            Device = snap,
        };
    }

    private TseSimulationDeviceSnapshotDto ToSnapshot(TseDevice device) =>
        new()
        {
            Id = device.Id,
            SerialNumber = device.SerialNumber,
            IsConnected = device.IsConnected,
            CanCreateInvoices = device.CanCreateInvoices,
            CertificateStatus = device.CertificateStatus,
            ExpiresAt = device.ExpiresAt,
            ErrorMessage = device.ErrorMessage,
            HealthScore = device.HealthScore,
            HealthStatus = device.HealthStatus.ToString(),
            SimulatedLatencyMs = _state.GetLatencyMs(device.Id),
            ActiveScenarioId = _state.GetActiveScenarioId(device.Id),
        };

    private static TseSimulationResultDto Denied(Guid deviceId) =>
        new()
        {
            Success = false,
            DeviceId = deviceId,
            Error = "TSE simulator is only available in Development.",
            Message = "Denied",
        };

    private static TseSimulationResultDto NotFound(Guid deviceId) =>
        new()
        {
            Success = false,
            DeviceId = deviceId,
            Error = "TSE device not found.",
            Message = "Not found",
        };

    private static TseSimulationScenarioDto Scenario(
        string id,
        string category,
        TseSimulatorFailureType type,
        string name,
        string description) =>
        new()
        {
            Id = id,
            Category = category,
            FailureType = type.ToString(),
            Name = name,
            Description = description,
        };

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
