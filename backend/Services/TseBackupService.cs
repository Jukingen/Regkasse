using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Backup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Creates / restores encrypted full TSE DR snapshots. Maps to real entities
/// (<see cref="TseDevice"/>, <see cref="SignatureChainState"/>, <see cref="ReceiptSequence"/>).
/// Signature chain counters live on chain/sequence tables — not on the device row.
/// Vendor private keys remain outside the package; device credential ciphertext may be included.
/// </summary>
public sealed class TseBackupService : ITseBackupService, ITseFullBackupService
{
    public const string ConfirmTokenValue = "RESTORE";
    public const string EncryptionBackupAes = "BackupAesGcm";
    public const string EncryptionDataProtection = "DataProtection";
    private const string DataProtectionPurpose = "KasseAPI.TseBackup.v1";
    private const string AuditEntityType = "TseBackup";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly AppDbContext _db;
    private readonly IBackupEncryptionService _backupEncryption;
    private readonly IDataProtector _dataProtector;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<TseBackupService> _logger;

    public TseBackupService(
        AppDbContext db,
        IBackupEncryptionService backupEncryption,
        IDataProtectionProvider dataProtectionProvider,
        IAuditLogService auditLog,
        ILogger<TseBackupService> logger)
    {
        _db = db;
        _backupEncryption = backupEncryption;
        _dataProtector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
        _auditLog = auditLog;
        _logger = logger;
    }

    Task<CreateTseBackupResponseDto> ITseFullBackupService.CreateFullBackupAsync(
        Guid tenantId,
        string? actorUserId,
        string? notes,
        CancellationToken cancellationToken) =>
        CreateTseBackupAsync(tenantId, actorUserId, notes, cancellationToken);

    Task<RestoreTseBackupResponseDto> ITseFullBackupService.RestoreFromBackupAsync(
        Guid backupId,
        RestoreTseBackupRequestDto request,
        string? actorUserId,
        CancellationToken cancellationToken) =>
        RestoreTseBackupAsync(backupId, request, actorUserId, cancellationToken);

    Task<IReadOnlyList<TseBackupListItemDto>> ITseFullBackupService.ListBackupsAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        ListBackupsAsync(tenantId, cancellationToken);

    public async Task<CreateTseBackupResponseDto> CreateTseBackupAsync(
        Guid tenantId,
        string? actorUserId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return new CreateTseBackupResponseDto { Success = false, Error = "tenantId is required." };

        var tenant = await _db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            return new CreateTseBackupResponseDto { Success = false, Error = "Tenant not found." };

        var registerIds = await _db.CashRegisters.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var devices = await LoadTenantDevicesAsync(tenantId, registerIds, asNoTracking: true, cancellationToken)
            .ConfigureAwait(false);

        var chains = await _db.SignatureChainState.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sequences = registerIds.Count == 0
            ? new List<ReceiptSequence>()
            : await _db.ReceiptSequences.AsNoTracking()
                .Where(s => registerIds.Contains(s.CashRegisterId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var backupId = Guid.NewGuid();
        var deviceSnaps = devices.Select(MapDevice).ToList();
        var defaultSigningId = ResolveDefaultSigningDeviceId(deviceSnaps);

        var payload = new TseBackupPayloadDto
        {
            SchemaVersion = TseBackupPayloadDto.CurrentSchemaVersion,
            BackupId = backupId,
            TenantId = tenantId,
            CreatedAtUtc = DateTime.UtcNow,
            Devices = deviceSnaps,
            SignatureChains = chains.Select(MapChain).ToList(),
            ReceiptSequences = sequences.Select(MapSequence).ToList(),
            Metadata = new TseBackupMetadataDto
            {
                TotalDevices = deviceSnaps.Count,
                PrimaryDevices = deviceSnaps.Count(d => d.IsPrimary),
                BackupDevices = deviceSnaps.Count(d => d.IsBackup),
                FailoverActiveDevices = deviceSnaps.Count(d => d.IsFailoverActive),
                EncryptionUsed = true,
                DefaultSigningDeviceId = defaultSigningId,
            },
        };

        if (!TryValidatePayload(payload, out var validationError))
            return new CreateTseBackupResponseDto { Success = false, Error = validationError };

        var json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var (blob, kind) = ProtectPayload(json);

        var record = new TseBackupRecord
        {
            Id = backupId,
            TenantId = tenantId,
            Payload = blob,
            EncryptionKind = kind,
            DeviceCount = payload.Devices.Count,
            ChainCount = payload.SignatureChains.Count,
            ReceiptSequenceCount = payload.ReceiptSequences.Count,
            CreatedBy = string.IsNullOrWhiteSpace(actorUserId) ? null : actorUserId.Trim(),
            CreatedAt = payload.CreatedAtUtc,
            SchemaVersion = payload.SchemaVersion,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()[..Math.Min(notes.Trim().Length, 256)],
        };

        _db.TseBackups.Add(record);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TryAuditAsync(
            "TSE_BACKUP_CREATED",
            actorUserId ?? "system",
            tenantId,
            backupId,
            description: $"Full TSE backup created ({record.DeviceCount} devices, {record.ChainCount} chains).",
            responseData: new
            {
                record.DeviceCount,
                record.ChainCount,
                record.ReceiptSequenceCount,
                record.EncryptionKind,
                record.SchemaVersion,
                payload.Metadata.PrimaryDevices,
                payload.Metadata.BackupDevices,
            },
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Full TSE backup {BackupId} created for tenant {TenantId} devices={Devices} chains={Chains} schema={Schema} enc={Enc}",
            backupId, tenantId, record.DeviceCount, record.ChainCount, record.SchemaVersion, record.EncryptionKind);

        return new CreateTseBackupResponseDto
        {
            Success = true,
            BackupId = backupId,
            Backup = ToListItem(record, tenant.Name, tenant.Slug),
        };
    }

    public async Task<IReadOnlyList<TseBackupListItemDto>> ListBackupsAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.TseBackups.AsNoTracking().IgnoreQueryFilters().AsQueryable();
        if (tenantId is { } tid && tid != Guid.Empty)
            query = query.Where(b => b.TenantId == tid);

        var rows = await query
            .OrderByDescending(b => b.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var tenantIds = rows.Select(r => r.TenantId).Distinct().ToList();
        var tenants = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(r =>
        {
            tenants.TryGetValue(r.TenantId, out var tenant);
            return ToListItem(r, tenant?.Name, tenant?.Slug);
        }).ToList();
    }

    public async Task<TseBackupRestorePreviewDto?> PreviewRestoreAsync(
        Guid backupId,
        CancellationToken cancellationToken = default)
    {
        var record = await _db.TseBackups.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == backupId, cancellationToken)
            .ConfigureAwait(false);
        if (record is null)
            return null;

        var payload = UnprotectPayload(record);
        var warnings = new List<string>
        {
            payload.CryptoMaterialNote,
            "Restore updates device inventory (incl. failover roles) and signature-chain counters only — not fiscal receipt rows.",
            "Startbeleg is never auto-created by restore.",
        };

        var liveChains = await _db.SignatureChainState.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.TenantId == record.TenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var registerIds = await _db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.TenantId == record.TenantId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var liveDevices = await LoadTenantDevicesAsync(record.TenantId, registerIds, asNoTracking: true, cancellationToken)
            .ConfigureAwait(false);

        var requireForce = false;
        foreach (var snap in payload.SignatureChains)
        {
            var live = liveChains.FirstOrDefault(c => c.CashRegisterId == snap.CashRegisterId);
            if (live is null)
                continue;
            if (live.LastCounter > snap.LastCounter
                || live.LastTurnoverCounterCents > snap.LastTurnoverCounterCents)
            {
                requireForce = true;
                warnings.Add(
                    $"Chain for register {snap.CashRegisterId:N} is ahead of backup "
                    + $"(live counter {live.LastCounter} > backup {snap.LastCounter}). ForceChainDowngrade required.");
            }
        }

        return new TseBackupRestorePreviewDto
        {
            BackupId = record.Id,
            TenantId = record.TenantId,
            BackupCreatedAt = record.CreatedAt,
            BackupDeviceCount = record.DeviceCount,
            BackupChainCount = record.ChainCount,
            LiveDeviceCount = liveDevices.Count,
            LiveChainCount = liveChains.Count,
            Warnings = warnings,
            WouldRequireForceDowngrade = requireForce,
            CryptoMaterialNote = payload.CryptoMaterialNote,
        };
    }

    public async Task<RestoreTseBackupResponseDto> RestoreTseBackupAsync(
        Guid backupId,
        RestoreTseBackupRequestDto request,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        request ??= new RestoreTseBackupRequestDto();
        if (!string.Equals(request.ConfirmToken, ConfirmTokenValue, StringComparison.Ordinal))
        {
            return new RestoreTseBackupResponseDto
            {
                Success = false,
                Error = $"confirmToken must be '{ConfirmTokenValue}'.",
            };
        }

        var record = await _db.TseBackups.IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == backupId, cancellationToken)
            .ConfigureAwait(false);
        if (record is null)
            return new RestoreTseBackupResponseDto { Success = false, Error = "Backup not found." };

        TseBackupPayloadDto payload;
        try
        {
            payload = UnprotectPayload(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt TSE backup {BackupId}", backupId);
            return new RestoreTseBackupResponseDto { Success = false, Error = "Failed to decrypt backup payload." };
        }

        if (payload.TenantId != record.TenantId)
        {
            return new RestoreTseBackupResponseDto
            {
                Success = false,
                Error = "Backup payload tenant mismatch.",
            };
        }

        if (!TryValidatePayload(payload, out var validationError))
            return new RestoreTseBackupResponseDto { Success = false, Error = validationError };

        var warnings = new List<string> { payload.CryptoMaterialNote };
        var devicesUpserted = 0;
        var chainsUpserted = 0;
        var chainsSkipped = 0;
        var sequencesUpserted = 0;

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Pass 1: upsert devices without PrimaryDeviceId FK (avoid missing parents).
            foreach (var snap in payload.Devices)
            {
                var device = await FindDeviceForSnapshotAsync(snap, record.TenantId, cancellationToken)
                    .ConfigureAwait(false);

                if (device is null)
                {
                    device = new TseDevice
                    {
                        Id = snap.DeviceId == Guid.Empty ? Guid.NewGuid() : snap.DeviceId,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = snap.IsActive,
                    };
                    _db.TseDevices.Add(device);
                }

                ApplyDeviceSnapshot(device, snap, record.TenantId, applyPrimaryLink: false);
                devicesUpserted++;
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Pass 2: apply primary/backup links + failover flags.
            foreach (var snap in payload.Devices)
            {
                var device = await FindDeviceForSnapshotAsync(snap, record.TenantId, cancellationToken)
                    .ConfigureAwait(false);
                if (device is null)
                    continue;
                ApplyDeviceSnapshot(device, snap, record.TenantId, applyPrimaryLink: true);
            }

            foreach (var snap in payload.SignatureChains)
            {
                var live = await _db.SignatureChainState.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(
                        c => c.TenantId == record.TenantId && c.CashRegisterId == snap.CashRegisterId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (live is not null
                    && (live.LastCounter > snap.LastCounter
                        || live.LastTurnoverCounterCents > snap.LastTurnoverCounterCents)
                    && !request.ForceChainDowngrade)
                {
                    chainsSkipped++;
                    warnings.Add(
                        $"Skipped chain restore for register {snap.CashRegisterId:N}: live ahead of backup.");
                    continue;
                }

                if (live is null)
                {
                    live = new SignatureChainState
                    {
                        Id = snap.Id == Guid.Empty ? Guid.NewGuid() : snap.Id,
                        TenantId = record.TenantId,
                        CashRegisterId = snap.CashRegisterId,
                    };
                    _db.SignatureChainState.Add(live);
                }

                live.LastSignature = snap.LastSignature;
                live.LastCounter = snap.LastCounter;
                live.LastTurnoverCounterCents = snap.LastTurnoverCounterCents;
                live.UpdatedAt = DateTime.UtcNow;
                chainsUpserted++;
            }

            foreach (var snap in payload.ReceiptSequences)
            {
                var live = await _db.ReceiptSequences
                    .FirstOrDefaultAsync(
                        s => s.CashRegisterId == snap.CashRegisterId
                             && s.SequenceDate.Date == snap.SequenceDate.Date,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (live is not null
                    && live.NextSequence > snap.NextSequence
                    && !request.ForceChainDowngrade)
                {
                    warnings.Add(
                        $"Skipped receipt sequence for register {snap.CashRegisterId:N} date {snap.SequenceDate:yyyy-MM-dd}: live ahead.");
                    continue;
                }

                if (live is null)
                {
                    live = new ReceiptSequence
                    {
                        Id = snap.Id == Guid.Empty ? Guid.NewGuid() : snap.Id,
                        CashRegisterId = snap.CashRegisterId,
                        SequenceDate = snap.SequenceDate.Date,
                    };
                    _db.ReceiptSequences.Add(live);
                }

                live.NextSequence = snap.NextSequence;
                live.UpdatedAt = DateTime.UtcNow;
                sequencesUpserted++;
            }

            await BindDefaultSigningDeviceAsync(record.TenantId, payload, cancellationToken)
                .ConfigureAwait(false);

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(ex, "TSE backup restore failed for {BackupId}", backupId);
            return new RestoreTseBackupResponseDto
            {
                Success = false,
                Error = "Restore transaction failed.",
                Detail = ex.Message,
            };
        }

        var detail =
            $"Restored devices={devicesUpserted}, chains={chainsUpserted}, skippedChains={chainsSkipped}, sequences={sequencesUpserted}.";

        await TryAuditAsync(
            "TSE_BACKUP_RESTORED",
            actorUserId ?? "system",
            record.TenantId,
            backupId,
            description: detail,
            responseData: new
            {
                devicesUpserted,
                chainsUpserted,
                chainsSkipped,
                sequencesUpserted,
                request.ForceChainDowngrade,
                SchemaVersion = payload.SchemaVersion,
            },
            cancellationToken).ConfigureAwait(false);

        _logger.LogWarning(
            "Full TSE backup {BackupId} restored for tenant {TenantId} by {Actor}: {Detail}",
            backupId, record.TenantId, actorUserId, detail);

        return new RestoreTseBackupResponseDto
        {
            Success = true,
            Detail = detail,
            DevicesUpserted = devicesUpserted,
            ChainsUpserted = chainsUpserted,
            ChainsSkipped = chainsSkipped,
            ReceiptSequencesUpserted = sequencesUpserted,
            Warnings = warnings,
        };
    }

    private async Task<List<TseDevice>> LoadTenantDevicesAsync(
        Guid tenantId,
        IReadOnlyList<Guid> registerIds,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        IQueryable<TseDevice> query = _db.TseDevices;
        if (asNoTracking)
            query = query.AsNoTracking();

        // Prefer TenantId; also include legacy rows linked only via KassenId / CashRegisterId.
        return await query
            .Where(d =>
                d.TenantId == tenantId
                || (registerIds.Count > 0 && (
                    registerIds.Contains(d.KassenId)
                    || (d.CashRegisterId != null && registerIds.Contains(d.CashRegisterId.Value)))))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TseDevice?> FindDeviceForSnapshotAsync(
        TseBackupDeviceSnapshotDto snap,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (snap.DeviceId != Guid.Empty)
        {
            var byId = await _db.TseDevices
                .FirstOrDefaultAsync(d => d.Id == snap.DeviceId, cancellationToken)
                .ConfigureAwait(false);
            if (byId is not null)
                return byId;
        }

        if (!string.IsNullOrWhiteSpace(snap.VendorDeviceId))
        {
            var byVendor = await _db.TseDevices
                .FirstOrDefaultAsync(
                    d => d.TenantId == tenantId && d.DeviceId == snap.VendorDeviceId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (byVendor is not null)
                return byVendor;
        }

        if (snap.KassenId != Guid.Empty)
        {
            return await _db.TseDevices
                .FirstOrDefaultAsync(
                    d => d.KassenId == snap.KassenId
                         && d.IsActive
                         && (d.TenantId == null || d.TenantId == tenantId)
                         && d.SerialNumber == snap.SerialNumber,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return null;
    }

    private async Task BindDefaultSigningDeviceAsync(
        Guid tenantId,
        TseBackupPayloadDto payload,
        CancellationToken cancellationToken)
    {
        var signingId = payload.Metadata?.DefaultSigningDeviceId
                        ?? ResolveDefaultSigningDeviceId(payload.Devices);
        if (signingId is null || signingId == Guid.Empty)
            return;

        var settings = await _db.CompanySettings.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (settings is null)
            return;

        settings.DefaultTseDeviceId = signingId.Value.ToString("D");
        settings.UpdatedAt = DateTime.UtcNow;
    }

    private static Guid? ResolveDefaultSigningDeviceId(IReadOnlyList<TseBackupDeviceSnapshotDto> devices)
    {
        var failover = devices.FirstOrDefault(d => d.IsFailoverActive && d.IsActive);
        if (failover is not null)
            return failover.DeviceId;

        var primary = devices.FirstOrDefault(d => d.IsPrimary && d.IsActive && !d.IsBackup);
        return primary?.DeviceId;
    }

    private static bool TryValidatePayload(TseBackupPayloadDto payload, out string? error)
    {
        if (payload.TenantId == Guid.Empty)
        {
            error = "Backup validation failed: tenantId is missing.";
            return false;
        }

        if (payload.SchemaVersion < 1 || payload.SchemaVersion > TseBackupPayloadDto.CurrentSchemaVersion)
        {
            error = $"Backup validation failed: unsupported schemaVersion {payload.SchemaVersion}.";
            return false;
        }

        if (payload.Devices is null)
        {
            error = "Backup validation failed: devices collection is null.";
            return false;
        }

        var ids = new HashSet<Guid>();
        foreach (var d in payload.Devices)
        {
            if (d.DeviceId != Guid.Empty && !ids.Add(d.DeviceId))
            {
                error = $"Backup validation failed: duplicate device id {d.DeviceId}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(d.SerialNumber))
            {
                error = "Backup validation failed: device serialNumber is required.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private (byte[] Blob, string Kind) ProtectPayload(ReadOnlySpan<byte> plaintext)
    {
        if (_backupEncryption.IsEnabled)
            return (_backupEncryption.Encrypt(plaintext), EncryptionBackupAes);

        return (_dataProtector.Protect(plaintext.ToArray()), EncryptionDataProtection);
    }

    private TseBackupPayloadDto UnprotectPayload(TseBackupRecord record)
    {
        byte[] plain = record.EncryptionKind switch
        {
            EncryptionBackupAes => _backupEncryption.Decrypt(record.Payload),
            EncryptionDataProtection => _dataProtector.Unprotect(record.Payload),
            _ when _backupEncryption.LooksEncrypted(record.Payload)
                => _backupEncryption.Decrypt(record.Payload),
            _ => record.Payload,
        };

        var payload = JsonSerializer.Deserialize<TseBackupPayloadDto>(plain, JsonOptions)
            ?? throw new InvalidOperationException("Backup payload JSON is empty.");
        payload.Metadata ??= new TseBackupMetadataDto();
        payload.Devices ??= new List<TseBackupDeviceSnapshotDto>();
        payload.SignatureChains ??= new List<TseBackupChainSnapshotDto>();
        payload.ReceiptSequences ??= new List<TseBackupReceiptSequenceSnapshotDto>();
        return payload;
    }

    private static TseBackupDeviceSnapshotDto MapDevice(TseDevice d) => new()
    {
        DeviceId = d.Id,
        TenantId = d.TenantId,
        KassenId = d.KassenId,
        CashRegisterId = d.CashRegisterId,
        VendorDeviceId = d.DeviceId,
        Provider = d.Provider,
        SerialNumber = d.SerialNumber,
        DeviceType = d.DeviceType,
        VendorId = d.VendorId,
        ProductId = d.ProductId,
        IsConnected = d.IsConnected,
        CanCreateInvoices = d.CanCreateInvoices,
        IsActive = d.IsActive,
        CertificateStatus = d.CertificateStatus,
        MemoryStatus = d.MemoryStatus,
        TimeoutSeconds = d.TimeoutSeconds,
        FinanzOnlineUsername = d.FinanzOnlineUsername,
        FinanzOnlineEnabled = d.FinanzOnlineEnabled,
        LastConnectionTime = d.LastConnectionTime,
        LastSignatureTime = d.LastSignatureTime,
        ErrorMessage = d.ErrorMessage,
        Certificate = d.Certificate,
        ApiKeyCiphertext = d.ApiKey,
        ApiSecretCiphertext = d.ApiSecret,
        IsPrimary = d.IsPrimary,
        IsBackup = d.IsBackup,
        PrimaryDeviceId = d.PrimaryDeviceId,
        IsFailoverActive = d.IsFailoverActive,
        HealthStatus = d.HealthStatus.ToString(),
        HealthScore = d.HealthScore,
        LastHealthCheck = d.LastHealthCheck,
        HealthMessage = d.HealthMessage,
        IssuedAt = d.IssuedAt,
        ExpiresAt = d.ExpiresAt,
        LastFailoverAt = d.LastFailoverAt,
        LastFailoverReason = d.LastFailoverReason,
        FailoverCount = d.FailoverCount,
    };

    private static TseBackupChainSnapshotDto MapChain(SignatureChainState c) => new()
    {
        Id = c.Id,
        CashRegisterId = c.CashRegisterId,
        LastSignature = c.LastSignature,
        LastCounter = c.LastCounter,
        LastTurnoverCounterCents = c.LastTurnoverCounterCents,
        UpdatedAt = c.UpdatedAt,
    };

    private static TseBackupReceiptSequenceSnapshotDto MapSequence(ReceiptSequence s) => new()
    {
        Id = s.Id,
        CashRegisterId = s.CashRegisterId,
        SequenceDate = s.SequenceDate,
        NextSequence = s.NextSequence,
        UpdatedAt = s.UpdatedAt,
    };

    private static void ApplyDeviceSnapshot(
        TseDevice device,
        TseBackupDeviceSnapshotDto snap,
        Guid tenantId,
        bool applyPrimaryLink)
    {
        device.TenantId ??= snap.TenantId ?? tenantId;
        device.KassenId = snap.KassenId != Guid.Empty
            ? snap.KassenId
            : snap.CashRegisterId ?? device.KassenId;
        device.CashRegisterId = snap.CashRegisterId ?? (snap.KassenId == Guid.Empty ? device.CashRegisterId : snap.KassenId);
        device.DeviceId = snap.VendorDeviceId ?? device.DeviceId;
        device.Provider = snap.Provider ?? device.Provider;
        device.SerialNumber = snap.SerialNumber;
        device.DeviceType = snap.DeviceType;
        device.VendorId = snap.VendorId;
        device.ProductId = snap.ProductId;
        device.IsConnected = snap.IsConnected;
        device.CanCreateInvoices = snap.CanCreateInvoices;
        device.IsActive = snap.IsActive;
        device.CertificateStatus = snap.CertificateStatus;
        device.MemoryStatus = snap.MemoryStatus;
        device.TimeoutSeconds = snap.TimeoutSeconds;
        device.FinanzOnlineUsername = snap.FinanzOnlineUsername;
        device.FinanzOnlineEnabled = snap.FinanzOnlineEnabled;
        device.LastConnectionTime = snap.LastConnectionTime;
        device.LastSignatureTime = snap.LastSignatureTime;
        device.ErrorMessage = snap.ErrorMessage;
        device.Certificate = snap.Certificate;
        if (snap.ApiKeyCiphertext is not null)
            device.ApiKey = snap.ApiKeyCiphertext;
        if (snap.ApiSecretCiphertext is not null)
            device.ApiSecret = snap.ApiSecretCiphertext;
        device.IsPrimary = snap.IsPrimary;
        device.IsBackup = snap.IsBackup;
        device.IsFailoverActive = snap.IsFailoverActive;
        device.HealthScore = snap.HealthScore;
        device.LastHealthCheck = snap.LastHealthCheck;
        device.HealthMessage = snap.HealthMessage;
        device.IssuedAt = snap.IssuedAt;
        device.ExpiresAt = snap.ExpiresAt;
        device.LastFailoverAt = snap.LastFailoverAt;
        device.LastFailoverReason = snap.LastFailoverReason;
        device.FailoverCount = snap.FailoverCount;
        if (Enum.TryParse<TseHealthStatus>(snap.HealthStatus, ignoreCase: true, out var health))
            device.HealthStatus = health;

        if (applyPrimaryLink)
        {
            // Only set FK when parent exists / is self-consistent; clear otherwise.
            if (snap.PrimaryDeviceId is { } parentId
                && parentId != Guid.Empty
                && parentId != device.Id)
            {
                device.PrimaryDeviceId = parentId;
            }
            else if (!snap.IsBackup)
            {
                device.PrimaryDeviceId = null;
            }
        }

        device.UpdatedAt = DateTime.UtcNow;
    }

    private static TseBackupListItemDto ToListItem(TseBackupRecord r, string? name, string? slug) => new()
    {
        Id = r.Id,
        TenantId = r.TenantId,
        TenantName = name,
        TenantSlug = slug,
        CreatedAt = r.CreatedAt,
        CreatedBy = r.CreatedBy,
        DeviceCount = r.DeviceCount,
        ChainCount = r.ChainCount,
        ReceiptSequenceCount = r.ReceiptSequenceCount,
        EncryptionKind = r.EncryptionKind,
        SchemaVersion = r.SchemaVersion,
        Notes = r.Notes,
    };

    private async Task TryAuditAsync(
        string action,
        string userId,
        Guid tenantId,
        Guid entityId,
        string description,
        object? responseData,
        CancellationToken cancellationToken)
    {
        try
        {
            await _auditLog.LogSystemOperationAsync(
                action,
                AuditEntityType,
                userId: userId,
                userRole: "SuperAdmin",
                description: description,
                status: AuditLogStatus.Success,
                responseData: responseData,
                entityId: entityId,
                tenantId: tenantId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TSE backup audit failed for {Action} {EntityId}", action, entityId);
        }

        _ = cancellationToken;
    }
}
