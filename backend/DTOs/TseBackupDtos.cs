using System.Text.Json.Serialization;

namespace KasseAPI_Final.DTOs;

/// <summary>JSON payload stored inside an encrypted <c>tse_backups</c> row.</summary>
public sealed class TseBackupPayloadDto
{
    /// <summary>v2 adds failover registry fields, tenant/cash-register links, metadata.</summary>
    public const int CurrentSchemaVersion = 2;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyName("backupId")]
    public Guid BackupId { get; set; }

    [JsonPropertyName("tenantId")]
    public Guid TenantId { get; set; }

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [JsonPropertyName("devices")]
    public List<TseBackupDeviceSnapshotDto> Devices { get; set; } = new();

    [JsonPropertyName("signatureChains")]
    public List<TseBackupChainSnapshotDto> SignatureChains { get; set; } = new();

    [JsonPropertyName("receiptSequences")]
    public List<TseBackupReceiptSequenceSnapshotDto> ReceiptSequences { get; set; } = new();

    [JsonPropertyName("metadata")]
    public TseBackupMetadataDto Metadata { get; set; } = new();

    /// <summary>
    /// Operator reminder: vendor private keys / fiskaly SCU secrets stay in config / key provider.
    /// Device-column credential ciphertext may be included when present (still encrypted at rest).
    /// </summary>
    [JsonPropertyName("cryptoMaterialNote")]
    public string CryptoMaterialNote { get; set; } =
        "Vendor private keys / fiskaly SCU secrets are NOT included. "
        + "Device ApiKey/ApiSecret ciphertext may be present when stored on the device row. "
        + "Restore config / vendor SCU separately when ciphertext is empty.";
}

/// <summary>Summary counts for a full TSE backup package.</summary>
public sealed class TseBackupMetadataDto
{
    [JsonPropertyName("totalDevices")]
    public int TotalDevices { get; set; }

    [JsonPropertyName("primaryDevices")]
    public int PrimaryDevices { get; set; }

    [JsonPropertyName("backupDevices")]
    public int BackupDevices { get; set; }

    [JsonPropertyName("failoverActiveDevices")]
    public int FailoverActiveDevices { get; set; }

    [JsonPropertyName("encryptionUsed")]
    public bool EncryptionUsed { get; set; } = true;

    /// <summary>Device id that should be bound as default signer after restore (if known).</summary>
    [JsonPropertyName("defaultSigningDeviceId")]
    public Guid? DefaultSigningDeviceId { get; set; }
}

public sealed class TseBackupDeviceSnapshotDto
{
    /// <summary>Persisted <c>TseDevices.Id</c> (row PK).</summary>
    [JsonPropertyName("deviceId")]
    public Guid DeviceId { get; set; }

    [JsonPropertyName("tenantId")]
    public Guid? TenantId { get; set; }

    [JsonPropertyName("kassenId")]
    public Guid KassenId { get; set; }

    [JsonPropertyName("cashRegisterId")]
    public Guid? CashRegisterId { get; set; }

    /// <summary>Vendor TSS / external device identifier string.</summary>
    [JsonPropertyName("vendorDeviceId")]
    public string? VendorDeviceId { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("serialNumber")]
    public string SerialNumber { get; set; } = string.Empty;

    [JsonPropertyName("deviceType")]
    public string DeviceType { get; set; } = string.Empty;

    [JsonPropertyName("vendorId")]
    public string VendorId { get; set; } = string.Empty;

    [JsonPropertyName("productId")]
    public string ProductId { get; set; } = string.Empty;

    [JsonPropertyName("isConnected")]
    public bool IsConnected { get; set; }

    [JsonPropertyName("canCreateInvoices")]
    public bool CanCreateInvoices { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("certificateStatus")]
    public string CertificateStatus { get; set; } = "UNKNOWN";

    [JsonPropertyName("memoryStatus")]
    public string MemoryStatus { get; set; } = "UNKNOWN";

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; }

    [JsonPropertyName("finanzOnlineUsername")]
    public string FinanzOnlineUsername { get; set; } = string.Empty;

    [JsonPropertyName("finanzOnlineEnabled")]
    public bool FinanzOnlineEnabled { get; set; }

    [JsonPropertyName("lastConnectionTime")]
    public DateTime LastConnectionTime { get; set; }

    [JsonPropertyName("lastSignatureTime")]
    public DateTime LastSignatureTime { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>Certificate metadata / thumbprint text (not private key).</summary>
    [JsonPropertyName("certificate")]
    public string? Certificate { get; set; }

    /// <summary>Already-encrypted API key ciphertext from the device row (optional).</summary>
    [JsonPropertyName("apiKeyCiphertext")]
    public string? ApiKeyCiphertext { get; set; }

    /// <summary>Already-encrypted API secret ciphertext from the device row (optional).</summary>
    [JsonPropertyName("apiSecretCiphertext")]
    public string? ApiSecretCiphertext { get; set; }

    [JsonPropertyName("isPrimary")]
    public bool IsPrimary { get; set; } = true;

    [JsonPropertyName("isBackup")]
    public bool IsBackup { get; set; }

    [JsonPropertyName("primaryDeviceId")]
    public Guid? PrimaryDeviceId { get; set; }

    [JsonPropertyName("isFailoverActive")]
    public bool IsFailoverActive { get; set; }

    [JsonPropertyName("healthStatus")]
    public string HealthStatus { get; set; } = "Healthy";

    [JsonPropertyName("healthScore")]
    public int HealthScore { get; set; } = 100;

    [JsonPropertyName("lastHealthCheck")]
    public DateTime? LastHealthCheck { get; set; }

    [JsonPropertyName("healthMessage")]
    public string? HealthMessage { get; set; }

    [JsonPropertyName("issuedAt")]
    public DateTime? IssuedAt { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    [JsonPropertyName("lastFailoverAt")]
    public DateTime? LastFailoverAt { get; set; }

    [JsonPropertyName("lastFailoverReason")]
    public string? LastFailoverReason { get; set; }

    [JsonPropertyName("failoverCount")]
    public int FailoverCount { get; set; }
}

public sealed class TseBackupChainSnapshotDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("cashRegisterId")]
    public Guid CashRegisterId { get; set; }

    [JsonPropertyName("lastSignature")]
    public string? LastSignature { get; set; }

    [JsonPropertyName("lastCounter")]
    public int LastCounter { get; set; }

    [JsonPropertyName("lastTurnoverCounterCents")]
    public long LastTurnoverCounterCents { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public sealed class TseBackupReceiptSequenceSnapshotDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("cashRegisterId")]
    public Guid CashRegisterId { get; set; }

    [JsonPropertyName("sequenceDate")]
    public DateTime SequenceDate { get; set; }

    [JsonPropertyName("nextSequence")]
    public int NextSequence { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public sealed class TseBackupListItemDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? TenantSlug { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public int DeviceCount { get; set; }
    public int ChainCount { get; set; }
    public int ReceiptSequenceCount { get; set; }
    public string EncryptionKind { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public string? Notes { get; set; }
}

public sealed class CreateTseBackupRequestDto
{
    public Guid TenantId { get; set; }
    public string? Notes { get; set; }
}

public sealed class CreateTseBackupResponseDto
{
    public bool Success { get; set; }
    public Guid? BackupId { get; set; }
    public string? Error { get; set; }
    public TseBackupListItemDto? Backup { get; set; }
}

public sealed class RestoreTseBackupRequestDto
{
    /// <summary>Must equal <c>RESTORE</c> (case-sensitive).</summary>
    public string ConfirmToken { get; set; } = string.Empty;

    /// <summary>
    /// When true, allow writing a lower LastCounter / NextSequence than live
    /// (dangerous — only after confirmed disaster wipe).
    /// </summary>
    public bool ForceChainDowngrade { get; set; }
}

public sealed class TseBackupRestorePreviewDto
{
    public Guid BackupId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime BackupCreatedAt { get; set; }
    public int BackupDeviceCount { get; set; }
    public int BackupChainCount { get; set; }
    public int LiveDeviceCount { get; set; }
    public int LiveChainCount { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    public bool WouldRequireForceDowngrade { get; set; }
    public string CryptoMaterialNote { get; set; } = string.Empty;
}

public sealed class RestoreTseBackupResponseDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Detail { get; set; }
    public int DevicesUpserted { get; set; }
    public int ChainsUpserted { get; set; }
    public int ChainsSkipped { get; set; }
    public int ReceiptSequencesUpserted { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}
