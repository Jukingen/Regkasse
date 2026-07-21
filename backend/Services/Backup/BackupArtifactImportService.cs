using System.Security.Cryptography;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

public sealed class BackupArtifactImportService : IBackupArtifactImportService
{
    public const string ImportedAdapterKind = "Imported";
    public const long DefaultMaxImportBytes = 2L * 1024 * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly IAuditLogService _audit;
    private readonly ILogger<BackupArtifactImportService> _logger;

    public BackupArtifactImportService(
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        IOptionsMonitor<BackupOptions> options,
        IAuditLogService audit,
        ILogger<BackupArtifactImportService> logger)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _options = options;
        _audit = audit;
        _logger = logger;
    }

    public async Task<BackupArtifactImportResponseDto> ImportAsync(
        BackupArtifactImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = _tenantAccessor.TenantId
            ?? throw new InvalidOperationException("TENANT_CONTEXT_REQUIRED");

        var tenant = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Id, t.Slug })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("TENANT_NOT_FOUND");

        var stagingRoot = _options.CurrentValue.ArtifactStagingRoot;
        if (string.IsNullOrWhiteSpace(stagingRoot))
            throw new InvalidOperationException("BACKUP_STORAGE_NOT_CONFIGURED");

        var rootFull = Path.GetFullPath(stagingRoot.Trim());
        Directory.CreateDirectory(rootFull);

        var utcNow = DateTime.UtcNow;
        var dumpFileName = SanitizeImportFileName(request.DumpFileName, tenant.Slug, utcNow, isManifest: false);
        var dumpAbsolute = Path.GetFullPath(Path.Combine(rootFull, dumpFileName));
        if (!BackupPathGuard.IsPathUnderStagingRoot(dumpAbsolute, rootFull))
            throw new InvalidOperationException("PATH_ESCAPE");

        if (request.DeclaredDumpLength is > DefaultMaxImportBytes)
            throw new ArgumentOutOfRangeException(nameof(request), "BACKUP_IMPORT_FILE_TOO_LARGE");

        var runId = Guid.NewGuid();
        var idempotencyKey = $"import-tenant-{tenantId:D}-{utcNow.Ticks}";

        long dumpBytes;
        string dumpHash;
        await using (var fileStream = new FileStream(
                         dumpAbsolute,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 81920,
                         FileOptions.Asynchronous))
        {
            (dumpBytes, dumpHash) = await CopyWithSha256AndLimitAsync(
                request.DumpStream,
                fileStream,
                DefaultMaxImportBytes,
                cancellationToken);
        }

        var dumpArtifact = new BackupArtifact
        {
            BackupRunId = runId,
            ArtifactType = BackupArtifactType.LogicalDump,
            StorageDescriptor = dumpFileName,
            ByteSize = dumpBytes,
            ContentHashSha256 = dumpHash,
            LifecycleState = BackupArtifactLifecycleState.Staging,
            MetadataJson = JsonSerializer.Serialize(new { format = "imported", role = "logical_dump" }),
        };

        BackupArtifact? manifestArtifact = null;
        string? manifestFileName = null;
        if (request.ManifestStream != null)
        {
            manifestFileName = SanitizeImportFileName(
                request.ManifestFileName ?? BackupArtifactFileNameBuilder.BuildManifestFileName(tenant.Slug, utcNow),
                tenant.Slug,
                utcNow,
                isManifest: true);
            var manifestAbsolute = Path.GetFullPath(Path.Combine(rootFull, manifestFileName));
            if (!BackupPathGuard.IsPathUnderStagingRoot(manifestAbsolute, rootFull))
                throw new InvalidOperationException("PATH_ESCAPE");

            long manifestBytes;
            string manifestHash;
            await using (var manifestOut = new FileStream(
                             manifestAbsolute,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             FileOptions.Asynchronous))
            {
                (manifestBytes, manifestHash) = await CopyWithSha256AndLimitAsync(
                    request.ManifestStream,
                    manifestOut,
                    10 * 1024 * 1024,
                    cancellationToken);
            }

            manifestArtifact = new BackupArtifact
            {
                BackupRunId = runId,
                ArtifactType = BackupArtifactType.VerificationManifest,
                StorageDescriptor = manifestFileName,
                ByteSize = manifestBytes,
                ContentHashSha256 = manifestHash,
                LifecycleState = BackupArtifactLifecycleState.Staging,
            };
        }

        var run = new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.OperatorApi,
            AdapterKind = ImportedAdapterKind,
            IdempotencyKey = idempotencyKey,
            Strategy = BackupStrategyKind.Tenant,
            TenantId = tenantId,
            RequestedByUserId = request.RequestedByUserId,
            RequestedAt = utcNow,
            QueuedAt = utcNow,
            StartedAt = utcNow,
            CompletedAt = utcNow,
            CorrelationId = request.CorrelationId,
            ConfigSnapshotJson = JsonSerializer.Serialize(new
            {
                source = "operator_import",
                tenantId,
                tenantSlug = tenant.Slug,
                originalDumpFileName = Path.GetFileName(request.DumpFileName),
            }),
            Artifacts = manifestArtifact == null
                ? new List<BackupArtifact> { dumpArtifact }
                : new List<BackupArtifact> { dumpArtifact, manifestArtifact },
        };

        _db.BackupRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogSystemOperationAsync(
            action: "BACKUP_ARTIFACT_IMPORTED",
            entityType: "BackupRun",
            userId: request.RequestedByUserId,
            userRole: request.RequestedByRole,
            description: $"Backup artifact imported for tenant {tenant.Slug} (run={runId}, file={dumpFileName}).",
            notes: "Import registers files for download/list only; no database restore was performed.",
            status: AuditLogStatus.Success,
            requestData: new
            {
                runId,
                dumpFileName,
                manifestFileName,
                tenantId,
                tenantSlug = tenant.Slug,
            },
            responseData: new { dumpArtifact.Id, manifestArtifactId = manifestArtifact?.Id },
            correlationIdOverride: request.CorrelationId,
            entityId: runId,
            tenantId: tenantId);

        _logger.LogInformation(
            "Backup artifact imported: runId={RunId}, tenantId={TenantId}, dumpBytes={DumpBytes}",
            runId,
            tenantId,
            dumpBytes);

        return new BackupArtifactImportResponseDto
        {
            BackupRunId = runId,
            ArtifactId = dumpArtifact.Id,
            FileName = dumpFileName,
            ByteSize = dumpBytes,
            ManifestArtifactId = manifestArtifact?.Id,
            ManifestFileName = manifestFileName,
        };
    }

    private static string SanitizeImportFileName(
        string originalName,
        string tenantSlug,
        DateTime timestampUtc,
        bool isManifest)
    {
        var baseName = Path.GetFileName(originalName.Trim());
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return isManifest
                ? BackupArtifactFileNameBuilder.BuildManifestFileName(tenantSlug, timestampUtc)
                : BackupArtifactFileNameBuilder.BuildLogicalDumpFileName(tenantSlug, timestampUtc);
        }

        var ext = Path.GetExtension(baseName);
        if (isManifest)
        {
            if (!ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
                return BackupArtifactFileNameBuilder.BuildManifestFileName(tenantSlug, timestampUtc);
        }
        else if (!ext.Equals(".dump", StringComparison.OrdinalIgnoreCase)
                 && !ext.Equals(".sql", StringComparison.OrdinalIgnoreCase))
        {
            return BackupArtifactFileNameBuilder.BuildLogicalDumpFileName(tenantSlug, timestampUtc);
        }

        return Path.GetFileName(baseName.Replace('\\', '/').Split('/').Last());
    }

    private static async Task<(long byteCount, string sha256Hex)> CopyWithSha256AndLimitAsync(
        Stream source,
        Stream destination,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
                throw new ArgumentOutOfRangeException(nameof(source), "BACKUP_IMPORT_FILE_TOO_LARGE");

            sha.TransformBlock(buffer, 0, read, null, 0);
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return (total, Convert.ToHexString(sha.Hash!).ToLowerInvariant());
    }
}
