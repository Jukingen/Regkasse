using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Automatic TSE device + signature-chain bootstrap for new tenants / cash registers.
/// Soft/Demo and Fake signing create a connected Fake/Soft device row (same pattern as demo reset).
/// Device mode binds a row to the register; when Fiskaly is configured the row is marked ready for signing.
/// Startbeleg is not created here — operators create it via RKSV special-receipt APIs after go-live readiness.
/// </summary>
public sealed class TseProvisioningService : ITseProvisioningService
{
    private const string AuditEntityType = "TseDevice";
    private const string ActionProvisioned = "TSE_PROVISIONED";
    private const string ActionSkipped = "TSE_PROVISIONING_SKIPPED";
    private const string ActionRevoked = "TSE_REVOKED";

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly IOptionsMonitor<FiskalyOptions> _fiskalyOptions;
    private readonly ITseProvider _tseProvider;
    private readonly ITseProviderFactory _tseProviderFactory;
    private readonly ITseHealthMonitor _healthMonitor;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<TseProvisioningService> _logger;

    public TseProvisioningService(
        AppDbContext db,
        IOptionsMonitor<TseOptions> tseOptions,
        IOptionsMonitor<FiskalyOptions> fiskalyOptions,
        ITseProvider tseProvider,
        ITseProviderFactory tseProviderFactory,
        ITseHealthMonitor healthMonitor,
        IAuditLogService auditLog,
        ILogger<TseProvisioningService> logger)
    {
        _db = db;
        _tseOptions = tseOptions;
        _fiskalyOptions = fiskalyOptions;
        _tseProvider = tseProvider;
        _tseProviderFactory = tseProviderFactory;
        _healthMonitor = healthMonitor;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<TseProvisioningResult> ProvisionTseForTenantAsync(
        Guid tenantId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return TseProvisioningResult.Fail("Tenant id is required.");

        var tenantExists = await _db.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!tenantExists)
            return TseProvisioningResult.Fail("Tenant not found");

        var opts = _tseOptions.CurrentValue;
        if (opts.IsOff || (!force && !opts.AutoProvisionOnTenantCreate))
        {
            var reason = opts.IsOff
                ? "TseMode=Off; TSE provisioning skipped."
                : "Tse:AutoProvisionOnTenantCreate=false; TSE provisioning skipped.";
            await TryAuditAsync(
                ActionSkipped,
                tenantId,
                null,
                reason,
                AuditLogStatus.Success,
                cancellationToken).ConfigureAwait(false);
            return TseProvisioningResult.Skipped(reason);
        }

        var cashRegister = await _db.CashRegisters
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && r.IsActive)
            .OrderByDescending(r => r.IsDefaultForTenant)
            .ThenBy(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (cashRegister is null)
            return TseProvisioningResult.Fail("No cash register found for tenant; create a register before TSE provisioning.");

        return await ProvisionTseForCashRegisterAsync(cashRegister.Id, force, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TseProvisioningResult> ProvisionTseForCashRegisterAsync(
        Guid cashRegisterId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (cashRegisterId == Guid.Empty)
            return TseProvisioningResult.Fail("Cash register id is required.");

        var opts = _tseOptions.CurrentValue;
        if (opts.IsOff || (!force && !opts.AutoProvisionOnTenantCreate))
        {
            var reason = opts.IsOff
                ? "TseMode=Off; TSE provisioning skipped."
                : "Tse:AutoProvisionOnTenantCreate=false; TSE provisioning skipped.";
            return TseProvisioningResult.Skipped(reason);
        }

        var cashRegister = await _db.CashRegisters
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId, cancellationToken)
            .ConfigureAwait(false);

        if (cashRegister is null)
            return TseProvisioningResult.Fail("Cash register not found");

        var tenantId = cashRegister.TenantId;

        var existing = await _db.TseDevices
            .FirstOrDefaultAsync(d => d.KassenId == cashRegisterId && d.IsActive, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            var chainReady = await EnsureSignatureChainAsync(tenantId, cashRegisterId, cancellationToken)
                .ConfigureAwait(false);
            await TryBindDefaultTseDeviceAsync(tenantId, existing.Id, cancellationToken).ConfigureAwait(false);

            return TseProvisioningResult.Success(
                existing,
                chainReady,
                detail: "TSE device already provisioned for this cash register.");
        }

        var now = DateTime.UtcNow;
        var (deviceType, serial, connected, canSign) = ResolveDeviceProfile(opts, cashRegister);

        var device = new TseDevice
        {
            Id = Guid.NewGuid(),
            SerialNumber = serial,
            DeviceType = deviceType,
            VendorId = "VID_PROVISIONED",
            ProductId = "PID_AUTO",
            IsConnected = connected,
            LastConnectionTime = now,
            LastSignatureTime = now,
            CertificateStatus = connected ? "VALID" : "UNKNOWN",
            MemoryStatus = "OK",
            CanCreateInvoices = canSign,
            TimeoutSeconds = 30,
            KassenId = cashRegisterId,
            FinanzOnlineUsername = string.Empty,
            FinanzOnlineEnabled = false,
            LastFinanzOnlineSync = now,
            PendingInvoices = 0,
            PendingReports = 0,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = "tse-provisioning",
            UpdatedBy = "tse-provisioning",
            IsActive = true,
        };

        _db.TseDevices.Add(device);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var chainInitialized = await EnsureSignatureChainAsync(tenantId, cashRegisterId, cancellationToken)
            .ConfigureAwait(false);

        await TryBindDefaultTseDeviceAsync(tenantId, device.Id, cancellationToken).ConfigureAwait(false);

        await TryAuditAsync(
            ActionProvisioned,
            tenantId,
            device.Id,
            $"TSE provisioned for register {cashRegister.RegisterNumber} ({deviceType}/{serial})",
            AuditLogStatus.Success,
            cancellationToken,
            new
            {
                DeviceId = device.Id,
                CashRegisterId = cashRegisterId,
                DeviceType = deviceType,
                SerialNumber = serial,
                IsConnected = connected,
                CanCreateInvoices = canSign,
                SignatureChainInitialized = chainInitialized,
                StartbelegCreated = false,
                Forced = force,
            }).ConfigureAwait(false);

        _logger.LogInformation(
            "Provisioned TSE device {DeviceId} type={DeviceType} for cash register {CashRegisterId} tenant {TenantId}",
            device.Id,
            deviceType,
            cashRegisterId,
            tenantId);

        return TseProvisioningResult.Success(
            device,
            chainInitialized,
            detail: $"TSE device provisioned ({deviceType}). Startbeleg must be created via RKSV special receipts when ready.");
    }

    public async Task<IReadOnlyList<TseDeviceFleetItemDto>> ListDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        var devices = await _db.TseDevices
            .AsNoTracking()
            .OrderByDescending(d => d.IsActive)
            .ThenBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (devices.Count == 0)
            return Array.Empty<TseDeviceFleetItemDto>();

        var registerIds = devices.Select(d => d.KassenId).Where(id => id != Guid.Empty).Distinct().ToList();
        var registers = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => registerIds.Contains(r.Id))
            .Select(r => new { r.Id, r.RegisterNumber, r.TenantId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var tenantIds = registers.Select(r => r.TenantId).Distinct().ToList();
        var tenants = await _db.Tenants
            .AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name, t.Slug })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var registerMap = registers.ToDictionary(r => r.Id);
        var tenantMap = tenants.ToDictionary(t => t.Id);

        return devices.Select(d =>
        {
            registerMap.TryGetValue(d.KassenId, out var reg);
            Guid? tenantId = reg?.TenantId;
            string? tenantName = null;
            string? tenantSlug = null;
            if (tenantId is { } tid && tenantMap.TryGetValue(tid, out var tenant))
            {
                tenantName = tenant.Name;
                tenantSlug = tenant.Slug;
            }

            var status = DeriveStatus(d);
            return new TseDeviceFleetItemDto
            {
                Id = d.Id,
                SerialNumber = d.SerialNumber,
                DeviceType = d.DeviceType,
                CashRegisterId = d.KassenId,
                CashRegisterNumber = reg?.RegisterNumber,
                TenantId = tenantId,
                TenantName = tenantName,
                TenantSlug = tenantSlug,
                Status = status,
                IsConnected = d.IsConnected,
                CanCreateInvoices = d.CanCreateInvoices,
                IsActive = d.IsActive,
                CertificateStatus = d.CertificateStatus,
                MemoryStatus = d.MemoryStatus,
                ErrorMessage = d.ErrorMessage,
                HealthScore = DeriveHealthScore(d, status),
                CreatedAt = d.CreatedAt,
                LastConnectionTime = d.LastConnectionTime,
                LastSignatureTime = d.LastSignatureTime,
            };
        }).ToList();
    }

    public async Task<TseFleetOverviewDto> GetFleetOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var devices = await ListDevicesAsync(cancellationToken).ConfigureAwait(false);
        var opts = _tseOptions.CurrentValue;
        var snap = _healthMonitor.Snapshot;
        var processScore = DeriveProcessHealthScore(snap.Status.ToString(), snap.ConsecutiveFailures);

        return new TseFleetOverviewDto
        {
            TotalDevices = devices.Count,
            ActiveDevices = devices.Count(d => d.Status == "Active"),
            DegradedDevices = devices.Count(d => d.Status == "Degraded"),
            InactiveDevices = devices.Count(d => d.Status == "Inactive"),
            ExpiredCertificateDevices = devices.Count(d => d.Status == "Expired"),
            ProcessHealthScore = processScore,
            ProcessHealthStatus = snap.Status.ToString(),
            ProcessLastCheckUtc = snap.LastCheckUtc,
            ProcessLastErrorSafe = snap.LastErrorMessageSafe,
            TseMode = opts.TseMode,
            SigningMode = opts.Mode,
            Devices = devices,
        };
    }

    public async Task<TseProvisioningResult> RevokeTseDeviceAsync(
        Guid deviceId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (deviceId == Guid.Empty)
            return TseProvisioningResult.Fail("Device id is required.");

        var device = await _db.TseDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
            .ConfigureAwait(false);

        if (device is null)
            return TseProvisioningResult.Fail("TSE device not found");

        if (!device.IsActive && !device.IsConnected && !device.CanCreateInvoices)
        {
            return TseProvisioningResult.Success(
                device,
                signatureChainInitialized: true,
                detail: "TSE device already revoked.");
        }

        Guid? tenantId = null;
        var register = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == device.KassenId, cancellationToken)
            .ConfigureAwait(false);
        tenantId = register?.TenantId;

        device.IsActive = false;
        device.IsConnected = false;
        device.CanCreateInvoices = false;
        device.UpdatedAt = DateTime.UtcNow;
        device.UpdatedBy = string.IsNullOrWhiteSpace(actorUserId) ? "tse-provisioning" : actorUserId;
        device.ErrorMessage = "Revoked by Super Admin";

        if (tenantId is { } tid)
        {
            var settings = await _db.CompanySettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tid, cancellationToken)
                .ConfigureAwait(false);
            if (settings is not null
                && string.Equals(settings.DefaultTseDeviceId, device.Id.ToString("D"), StringComparison.OrdinalIgnoreCase))
            {
                settings.DefaultTseDeviceId = null;
                settings.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TryAuditAsync(
            ActionRevoked,
            tenantId ?? Guid.Empty,
            device.Id,
            $"TSE device revoked ({device.SerialNumber})",
            AuditLogStatus.Success,
            cancellationToken,
            new { DeviceId = device.Id, CashRegisterId = device.KassenId, Actor = actorUserId }).ConfigureAwait(false);

        _logger.LogInformation(
            "Revoked TSE device {DeviceId} cashRegister={CashRegisterId} actor={Actor}",
            device.Id,
            device.KassenId,
            actorUserId);

        return TseProvisioningResult.Success(device, signatureChainInitialized: true, detail: "TSE device revoked.");
    }

    public async Task<TseProvisioningStatus> GetTseStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var opts = _tseOptions.CurrentValue;
        var registers = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var registerIds = registers.ToHashSet();
        var devices = registerIds.Count == 0
            ? new List<TseDevice>()
            : await _db.TseDevices
                .AsNoTracking()
                .Where(d => registerIds.Contains(d.KassenId))
                .OrderBy(d => d.CreatedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var chainCount = registerIds.Count == 0
            ? 0
            : await _db.SignatureChainState
                .IgnoreQueryFilters()
                .AsNoTracking()
                .CountAsync(s => s.TenantId == tenantId && registerIds.Contains(s.CashRegisterId), cancellationToken)
                .ConfigureAwait(false);

        var primary = devices.FirstOrDefault(d => d.IsActive) ?? devices.FirstOrDefault();
        var active = devices.Count(d => d.IsActive);
        var connected = devices.Count(d => d.IsActive && d.IsConnected);
        var operational = !opts.IsOff
            && (opts.UseSoftTseWhenNoDevice || opts.IsFakeSigningMode || connected > 0);

        string status;
        string? message;
        if (opts.IsOff)
        {
            status = "Off";
            message = "TSE is disabled (TseMode=Off).";
        }
        else if (devices.Count == 0)
        {
            status = "NotProvisioned";
            message = "No TSE device rows for this tenant's cash registers.";
        }
        else if (operational)
        {
            status = "Operational";
            message = null;
        }
        else
        {
            status = "PendingConnection";
            message = "TSE device row exists but is not connected / cannot sign yet.";
        }

        return new TseProvisioningStatus
        {
            TenantId = tenantId,
            TseMode = opts.TseMode,
            SigningMode = opts.Mode,
            IsOff = opts.IsOff,
            DeviceCount = devices.Count,
            ActiveDeviceCount = active,
            ConnectedDeviceCount = connected,
            RegistersWithChainState = chainCount,
            CashRegisterCount = registers.Count,
            PrimaryDeviceId = primary?.Id,
            PrimarySerialNumber = primary?.SerialNumber,
            PrimaryDeviceType = primary?.DeviceType,
            IsOperational = operational,
            Status = status,
            Message = message,
        };
    }

    public async Task<TseProvisioningHealthCheck> PerformHealthCheckAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var status = await GetTseStatusAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var opts = _tseOptions.CurrentValue;

        if (opts.IsOff)
        {
            return new TseProvisioningHealthCheck
            {
                TenantId = tenantId,
                IsHealthy = true,
                CheckedAtUtc = now,
                Status = "Off",
                Detail = "TSE disabled; health check treated as healthy.",
                ProviderReady = true,
            };
        }

        var providerReady = false;
        try
        {
            providerReady = await _tseProvider.IsReadyAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TSE provider readiness check failed for tenant {TenantId}", tenantId);
        }

        // Soft/Fake modes: local software path does not require a connected hardware probe.
        if (opts.UseSoftTseWhenNoDevice || opts.IsFakeSigningMode)
            providerReady = true;

        var healthy = status.DeviceCount > 0
            && status.IsOperational
            && (providerReady || opts.UseSoftTseWhenNoDevice || opts.IsFakeSigningMode);

        return new TseProvisioningHealthCheck
        {
            TenantId = tenantId,
            IsHealthy = healthy,
            CheckedAtUtc = now,
            Status = healthy ? "Healthy" : status.Status,
            Detail = healthy
                ? null
                : status.Message ?? "TSE provisioning health check failed.",
            DeviceId = status.PrimaryDeviceId,
            SerialNumber = status.PrimarySerialNumber,
            ProviderReady = providerReady,
        };
    }

    private (string DeviceType, string Serial, bool Connected, bool CanSign) ResolveDeviceProfile(
        TseOptions opts,
        CashRegister cashRegister)
    {
        var explicitProvider = TseOptions.NormalizeProviderName(opts.Provider);
        var providerLabel = string.IsNullOrEmpty(explicitProvider)
            ? TseOptions.NormalizeProviderName(_tseProviderFactory.ResolveConfiguredProviderName())
            : explicitProvider;

        if (opts.IsFakeSigningMode
            || providerLabel is TseOptions.ProviderFake
            || (opts.UseSoftTseWhenNoDevice && providerLabel is TseOptions.ProviderSoft or ""))
        {
            var softType = opts.IsFakeSigningMode || providerLabel is TseOptions.ProviderFake ? "Fake" : "Soft";
            var serial = Truncate($"AUTO-{softType}-{cashRegister.RegisterNumber}-{cashRegister.Id:N}", 100);
            return (softType, serial, Connected: true, CanSign: true);
        }

        if (opts.UseSoftTseWhenNoDevice && providerLabel is not TseOptions.ProviderFiskaly
            and not TseOptions.ProviderEpson and not TseOptions.ProviderSwissbit)
        {
            var serial = Truncate($"AUTO-Soft-{cashRegister.RegisterNumber}-{cashRegister.Id:N}", 100);
            return ("Soft", serial, Connected: true, CanSign: true);
        }

        if (providerLabel is TseOptions.ProviderEpson or TseOptions.ProviderSwissbit)
        {
            // Stubs only — never mark as ready until a real vendor adapter ships.
            var pendingSerial = Truncate(
                $"{providerLabel.ToUpperInvariant()}-PENDING-{cashRegister.RegisterNumber}-{cashRegister.Id:N}",
                100);
            return (providerLabel, pendingSerial, Connected: false, CanSign: false);
        }

        var fiskaly = _fiskalyOptions.CurrentValue;
        var fiskalyReady = _tseProviderFactory.IsProviderConfigured(TseOptions.ProviderFiskaly)
            || fiskaly.IsConfigured;
        // Only stamp DeviceType=fiskaly when credentials exist or Provider was set explicitly.
        if (fiskalyReady || explicitProvider is TseOptions.ProviderFiskaly)
        {
            var vendor = opts.GetVendorConnection(TseOptions.ProviderFiskaly);
            var scu = !string.IsNullOrWhiteSpace(fiskaly.SignatureCreationUnitId)
                ? fiskaly.SignatureCreationUnitId
                : vendor?.SignatureCreationUnitId;
            if (string.IsNullOrWhiteSpace(scu))
                scu = $"fiskaly-{cashRegister.Id:N}";

            return ("fiskaly", Truncate(scu.Trim(), 100), Connected: fiskalyReady, CanSign: fiskalyReady);
        }

        // Device mode without vendor credentials — pending placeholder (operator wires hardware later).
        var deviceType = string.IsNullOrEmpty(explicitProvider) ? "Device" : explicitProvider;
        return (deviceType, Truncate($"PENDING-{cashRegister.RegisterNumber}-{cashRegister.Id:N}", 100),
            Connected: false, CanSign: false);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private async Task<bool> EnsureSignatureChainAsync(
        Guid tenantId,
        Guid cashRegisterId,
        CancellationToken cancellationToken)
    {
        var exists = await _db.SignatureChainState
            .IgnoreQueryFilters()
            .AnyAsync(s => s.CashRegisterId == cashRegisterId, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
            return true;

        _db.SignatureChainState.Add(new SignatureChainState
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = cashRegisterId,
            LastSignature = null,
            LastCounter = 0,
            LastTurnoverCounterCents = 0,
            UpdatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task TryBindDefaultTseDeviceAsync(
        Guid tenantId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _db.CompanySettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
                .ConfigureAwait(false);

            if (settings is null)
                return;

            if (!string.IsNullOrWhiteSpace(settings.DefaultTseDeviceId))
                return;

            settings.DefaultTseDeviceId = deviceId.ToString("D");
            settings.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to bind DefaultTseDeviceId for tenant {TenantId} device {DeviceId}",
                tenantId,
                deviceId);
        }
    }

    private async Task TryAuditAsync(
        string action,
        Guid tenantId,
        Guid? deviceId,
        string description,
        AuditLogStatus status,
        CancellationToken cancellationToken,
        object? responseData = null)
    {
        try
        {
            await _auditLog.LogSystemOperationAsync(
                action,
                AuditEntityType,
                userId: "system",
                userRole: "System",
                description: description,
                status: status,
                responseData: responseData,
                entityId: deviceId,
                tenantId: tenantId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TSE provisioning audit failed for action {Action} tenant {TenantId}", action, tenantId);
        }

        _ = cancellationToken;
    }

    private static string DeriveStatus(TseDevice d)
    {
        if (string.Equals(d.CertificateStatus, "EXPIRED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.CertificateStatus, "REVOKED", StringComparison.OrdinalIgnoreCase))
            return "Expired";

        if (!d.IsActive)
            return "Inactive";

        if (d.IsConnected && d.CanCreateInvoices)
            return "Active";

        return "Degraded";
    }

    private static int DeriveHealthScore(TseDevice d, string status) =>
        status switch
        {
            "Active" => 100,
            "Degraded" => d.IsActive ? 45 : 20,
            "Expired" => 10,
            "Inactive" => 0,
            _ => 30,
        };

    private static int DeriveProcessHealthScore(string status, int consecutiveFailures)
    {
        if (string.Equals(status, "Online", StringComparison.OrdinalIgnoreCase))
            return 100;
        if (string.Equals(status, "Offline", StringComparison.OrdinalIgnoreCase))
            return Math.Max(0, Math.Min(20, 15 - Math.Min(consecutiveFailures, 10)));
        if (string.Equals(status, "Degraded", StringComparison.OrdinalIgnoreCase))
            return Math.Max(30, 100 - Math.Min(consecutiveFailures * 15, 65));
        return 55;
    }
}
