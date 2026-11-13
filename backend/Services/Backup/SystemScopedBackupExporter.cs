using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Super Admin system backup package metadata (Identity + all tenants + platform).
/// Per-tenant row payloads live under <c>tenants/*.tenant.zip</c> entries — not in this object.
/// </summary>
public sealed class SystemBackupData
{
    public string Format { get; init; } = "regkasse.system-backup.v1";
    public DateTime ExportedAtUtc { get; init; }
    public int ActiveTenantCount { get; init; }
    public IReadOnlyList<Guid> ActiveTenantIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyDictionary<string, int> SectionRowCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public IReadOnlyList<string> IncludedCategories { get; init; } =
        new[]
        {
            "all_active_tenants",
            "identity_users",
            "platform_settings",
            "deployment_license",
            "audit_logs"
        };
}

public interface ISystemScopedBackupExporter
{
    /// <summary>
    /// Writes a Super Admin system ZIP: Identity, licenses, platform settings, audit,
    /// plus nested tenant packages for every active tenant.
    /// </summary>
    Task<SystemScopedBackupExportResult> ExportAsync(
        AppDbContext db,
        string absoluteZipPath,
        CancellationToken ct = default);
}

public sealed class SystemScopedBackupExportResult
{
    public required SystemBackupData Manifest { get; init; }
    public long ByteSize { get; init; }
}

/// <summary>
/// Builds <c>*.system.zip</c> for Super Admin DR / portable export (not a substitute for pg_restore alone).
/// </summary>
public sealed class SystemScopedBackupExporter : ISystemScopedBackupExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false
    };

    private readonly ITenantScopedBackupExporter _tenantExporter;
    private readonly ICompressionService _compression;

    public SystemScopedBackupExporter(
        ITenantScopedBackupExporter tenantExporter,
        ICompressionService? compression = null)
    {
        _tenantExporter = tenantExporter;
        _compression = compression ?? CompressionService.Shared;
    }

    public async Task<SystemScopedBackupExportResult> ExportAsync(
        AppDbContext db,
        string absoluteZipPath,
        CancellationToken ct = default)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var dir = Path.GetDirectoryName(absoluteZipPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(absoluteZipPath))
            File.Delete(absoluteZipPath);

        var tenants = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Slug)
            .ToListAsync(ct);

        await using (var zipStream = new FileStream(
                         absoluteZipPath,
                         FileMode.CreateNew,
                         FileAccess.ReadWrite,
                         FileShare.None,
                         64 * 1024,
                         FileOptions.Asynchronous))
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false))
        {
            // ✅ All active tenants (metadata)
            await WriteJsonEntryAsync(zip, "tenants.json", tenants.Select(t => new
            {
                t.Id,
                t.Name,
                t.Slug,
                t.Status,
                t.IsActive,
                t.Email,
                t.CreatedAt
            }), ct);
            counts["tenants.json"] = tenants.Count;

            // ✅ Identity users + related tables (credentials required for Super Admin DR)
            var users = await db.Users.AsNoTracking().ToListAsync(ct);
            await WriteJsonEntryAsync(zip, "identity/users.json", users, ct);
            counts["identity/users.json"] = users.Count;

            var roles = await db.Roles.AsNoTracking().ToListAsync(ct);
            await WriteJsonEntryAsync(zip, "identity/roles.json", roles, ct);
            counts["identity/roles.json"] = roles.Count;

            var userRoles = await db.UserRoles.AsNoTracking().ToListAsync(ct);
            await WriteJsonEntryAsync(zip, "identity/user_roles.json", userRoles, ct);
            counts["identity/user_roles.json"] = userRoles.Count;

            var userClaims = await db.UserClaims.AsNoTracking().ToListAsync(ct);
            await WriteJsonEntryAsync(zip, "identity/user_claims.json", userClaims, ct);
            counts["identity/user_claims.json"] = userClaims.Count;

            var userLogins = await db.UserLogins.AsNoTracking().ToListAsync(ct);
            await WriteJsonEntryAsync(zip, "identity/user_logins.json", userLogins, ct);
            counts["identity/user_logins.json"] = userLogins.Count;

            var userTokens = await db.UserTokens.AsNoTracking().ToListAsync(ct);
            await WriteJsonEntryAsync(zip, "identity/user_tokens.json", userTokens, ct);
            counts["identity/user_tokens.json"] = userTokens.Count;

            var memberships = await db.UserTenantMemberships.AsNoTracking()
                .IgnoreQueryFilters()
                .ToListAsync(ct);
            await WriteJsonEntryAsync(zip, "identity/user_tenant_memberships.json", memberships, ct);
            counts["identity/user_tenant_memberships.json"] = memberships.Count;

            // ✅ Platform / system settings
            var ntp = await db.NtpAdminSettings.AsNoTracking().ToListAsync(ct);
            await WriteJsonEntryAsync(zip, "platform/ntp_admin_settings.json", ntp, ct);
            counts["platform/ntp_admin_settings.json"] = ntp.Count;

            var backupSettings = await db.BackupSettings.AsNoTracking().ToListAsync(ct);
            await WriteJsonEntryAsync(zip, "platform/backup_settings.json", backupSettings, ct);
            counts["platform/backup_settings.json"] = backupSettings.Count;

            // ✅ Deployment license
            var licenses = await db.ActivatedLicenses.AsNoTracking().ToListAsync(ct);
            await WriteJsonEntryAsync(zip, "platform/activated_licenses.json", licenses, ct);
            counts["platform/activated_licenses.json"] = licenses.Count;

            // ✅ All audit logs (deployment-wide)
            var auditLogs = await db.AuditLogs.AsNoTracking().IgnoreQueryFilters().ToListAsync(ct);
            await WriteJsonEntryAsync(zip, "audit_logs.json", auditLogs, ct);
            counts["audit_logs.json"] = auditLogs.Count;

            // ✅ All tenant data (nested tenant packages)
            var tempRoot = Path.Combine(Path.GetTempPath(), "regkasse-system-backup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                foreach (var tenant in tenants)
                {
                    ct.ThrowIfCancellationRequested();
                    var tenantZip = Path.Combine(tempRoot, $"{tenant.Slug}.tenant.zip");
                    var tenantExport = await _tenantExporter.ExportAsync(
                        db,
                        tenant.Id,
                        tenant.Slug,
                        tenantZip,
                        ct);
                    var entryName = $"tenants/{BackupArtifactFileNameBuilder.SanitizeSlugPublic(tenant.Slug)}.tenant.zip";
                    zip.CreateEntryFromFile(tenantZip, entryName, _compression.ResolveZipEntryLevel(entryName));
                    counts[entryName] = tenantExport.Manifest.TableRowCounts.Values.Sum();
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                        Directory.Delete(tempRoot, recursive: true);
                }
                catch
                {
                    // best-effort cleanup
                }
            }

            var manifest = new SystemBackupData
            {
                ExportedAtUtc = DateTime.UtcNow,
                ActiveTenantCount = tenants.Count,
                ActiveTenantIds = tenants.Select(t => t.Id).ToArray(),
                SectionRowCounts = counts
            };
            await WriteJsonEntryAsync(zip, "manifest.json", manifest, ct);
        }

        var fi = new FileInfo(absoluteZipPath);
        return new SystemScopedBackupExportResult
        {
            Manifest = new SystemBackupData
            {
                ExportedAtUtc = DateTime.UtcNow,
                ActiveTenantCount = tenants.Count,
                ActiveTenantIds = tenants.Select(t => t.Id).ToArray(),
                SectionRowCounts = counts
            },
            ByteSize = fi.Exists ? fi.Length : 0
        };
    }

    private async Task WriteJsonEntryAsync<T>(
        ZipArchive zip,
        string entryName,
        T payload,
        CancellationToken ct)
    {
        var entry = zip.CreateEntry(entryName, _compression.ResolveZipEntryLevel(entryName));
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, ct);
    }
}
