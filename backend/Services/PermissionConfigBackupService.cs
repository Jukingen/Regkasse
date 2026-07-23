using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class PermissionConfigBackupService : IPermissionConfigBackupService
{
    public const string NotFoundCode = "BACKUP_NOT_FOUND";
    public const string InvalidPayloadCode = "INVALID_BACKUP_PAYLOAD";
    private const int RetentionCount = 30;
    private static readonly TimeSpan AutoBackupDebounce = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly AppDbContext _db;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IRoleManagementService _roleManagement;
    private readonly IAuditLogService _audit;
    private readonly TimeProvider _time;
    private readonly ILogger<PermissionConfigBackupService> _logger;
    private static DateTime? _lastAutoBackupUtc;
    private static readonly object AutoBackupGate = new();

    public PermissionConfigBackupService(
        AppDbContext db,
        RoleManager<IdentityRole> roleManager,
        IRoleManagementService roleManagement,
        IAuditLogService audit,
        TimeProvider time,
        ILogger<PermissionConfigBackupService> logger)
    {
        _db = db;
        _roleManager = roleManager;
        _roleManagement = roleManagement;
        _audit = audit;
        _time = time;
        _logger = logger;
    }

    public async Task<PermissionConfigBackupListItemDto> CreateAsync(
        CreatePermissionConfigBackupRequest? request,
        string? actorUserId,
        string trigger = PermissionConfigBackupTriggers.Manual,
        CancellationToken cancellationToken = default)
    {
        var payload = await BuildPayloadAsync(cancellationToken);
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var now = _time.GetUtcNow().UtcDateTime;
        var entity = new PermissionConfigBackup
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(request?.Name)
                ? $"Permission config {now:yyyy-MM-dd HH:mm} UTC"
                : request!.Name!.Trim(),
            Note = string.IsNullOrWhiteSpace(request?.Note) ? null : request!.Note!.Trim(),
            CreatedAt = now,
            CreatedByUserId = actorUserId,
            Trigger = trigger,
            PayloadJson = json,
            PayloadHash = Sha256Hex(json),
            SchemaVersion = 1,
        };

        _db.PermissionConfigBackups.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        await EnforceRetentionAsync(cancellationToken);

        try
        {
            await _audit.LogSystemOperationAsync(
                "PERMISSION_CONFIG_BACKUP_CREATED",
                "PermissionConfigBackup",
                actorUserId ?? "system",
                Roles.SuperAdmin,
                description: $"Permission config backup created: {entity.Name}",
                requestData: new { entity.Id, entity.Trigger, entity.PayloadHash },
                actionType: AuditEventType.PermissionConfigBackupCreated,
                entityId: entity.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to audit permission config backup create");
        }

        return MapListItem(entity);
    }

    public async Task<IReadOnlyList<PermissionConfigBackupListItemDto>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.PermissionConfigBackups.AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        return rows.Select(MapListItem).ToList();
    }

    public async Task<PermissionConfigRestorePreviewDto?> PreviewRestoreAsync(
        Guid backupId,
        CancellationToken cancellationToken = default)
    {
        var backup = await _db.PermissionConfigBackups.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == backupId, cancellationToken);
        if (backup is null)
            return null;

        if (!TryDeserialize(backup.PayloadJson, out var payload) || payload is null)
        {
            return new PermissionConfigRestorePreviewDto
            {
                BackupId = backupId,
                Warnings = new[] { "Backup payload could not be parsed." },
            };
        }

        var current = await BuildPayloadAsync(cancellationToken);
        var warnings = new List<string>();
        var sample = new List<string>();

        var currentRoleNames = current.CustomRoles.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var backupRoleNames = payload.CustomRoles.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rolesChanged = 0;
        foreach (var role in payload.CustomRoles)
        {
            var match = current.CustomRoles.FirstOrDefault(r =>
                string.Equals(r.Name, role.Name, StringComparison.OrdinalIgnoreCase));
            if (match is null
                || !match.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase)
                    .SetEquals(role.Permissions))
            {
                rolesChanged++;
                if (sample.Count < 8)
                    sample.Add($"role:{role.Name}");
            }
        }

        foreach (var name in currentRoleNames.Except(backupRoleNames, StringComparer.OrdinalIgnoreCase))
        {
            rolesChanged++;
            warnings.Add($"Current custom role '{name}' is not in backup (will keep unless deleted manually).");
        }

        var packagesChanged = Math.Abs(current.Packages.Count - payload.Packages.Count)
            + current.Packages.Count(p =>
                payload.Packages.All(b => !string.Equals(b.Slug, p.Slug, StringComparison.OrdinalIgnoreCase)));
        var overridesChanged = Math.Abs(current.Overrides.Count - payload.Overrides.Count);

        return new PermissionConfigRestorePreviewDto
        {
            BackupId = backupId,
            CustomRolesChanged = rolesChanged,
            PackagesChanged = packagesChanged,
            OverridesChanged = overridesChanged,
            Warnings = warnings,
            SampleRoleDeltas = sample,
        };
    }

    public async Task<(bool Succeeded, string? Code, string? Error)> RestoreAsync(
        Guid backupId,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var backup = await _db.PermissionConfigBackups
            .FirstOrDefaultAsync(b => b.Id == backupId, cancellationToken);
        if (backup is null)
            return (false, NotFoundCode, "Backup not found.");

        if (!TryDeserialize(backup.PayloadJson, out var payload) || payload is null)
            return (false, InvalidPayloadCode, "Backup payload is invalid.");

        await CreateAsync(
            new CreatePermissionConfigBackupRequest
            {
                Name = $"Pre-restore {_time.GetUtcNow().UtcDateTime:yyyy-MM-dd HH:mm} UTC",
                Note = $"Auto backup before restore of {backup.Id}",
            },
            actorUserId,
            PermissionConfigBackupTriggers.AutoBeforeChange,
            cancellationToken);

        // Restore custom role claims
        foreach (var roleSnap in payload.CustomRoles)
        {
            if (Roles.Canonical.Contains(roleSnap.Name, StringComparer.OrdinalIgnoreCase))
                continue;

            var role = await _roleManager.FindByNameAsync(roleSnap.Name);
            if (role is null)
            {
                await _roleManagement.CreateRoleAsync(roleSnap.Name, inheritFromRole: null, cancellationToken);
            }

            await _roleManagement.SetRolePermissionsAsync(roleSnap.Name, roleSnap.Permissions, cancellationToken);
        }

        // Restore packages (custom only replace-by-slug; keep system via seed)
        var systemSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "reporting", "cash-operations", "user-management",
        };
        var existingPackages = await _db.PermissionPackages
            .Include(p => p.Keys)
            .ToListAsync(cancellationToken);

        foreach (var pkg in existingPackages.Where(p => !p.IsSystem && !systemSlugs.Contains(p.Slug)).ToList())
        {
            if (payload.Packages.All(p => !string.Equals(p.Slug, pkg.Slug, StringComparison.OrdinalIgnoreCase)))
            {
                var assignments = await _db.RolePermissionPackages
                    .Where(a => a.PackageId == pkg.Id)
                    .ToListAsync(cancellationToken);
                _db.RolePermissionPackages.RemoveRange(assignments);
                _db.PermissionPackages.Remove(pkg);
            }
        }

        foreach (var snap in payload.Packages)
        {
            if (snap.IsSystem || systemSlugs.Contains(snap.Slug))
                continue;

            var entity = existingPackages.FirstOrDefault(p =>
                string.Equals(p.Slug, snap.Slug, StringComparison.OrdinalIgnoreCase));
            if (entity is null)
            {
                entity = new PermissionPackage
                {
                    Id = Guid.NewGuid(),
                    Slug = snap.Slug,
                    Name = snap.Name,
                    Description = snap.Description,
                    IsSystem = false,
                    CreatedAt = _time.GetUtcNow().UtcDateTime,
                    UpdatedAt = _time.GetUtcNow().UtcDateTime,
                };
                _db.PermissionPackages.Add(entity);
                existingPackages.Add(entity);
            }
            else
            {
                entity.Name = snap.Name;
                entity.Description = snap.Description;
                entity.UpdatedAt = _time.GetUtcNow().UtcDateTime;
                _db.PermissionPackageKeys.RemoveRange(entity.Keys);
                entity.Keys.Clear();
            }

            foreach (var key in snap.Permissions.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                entity.Keys.Add(new PermissionPackageKey
                {
                    Id = Guid.NewGuid(),
                    PackageId = entity.Id,
                    Permission = key,
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Role ↔ package assignments
        var allAssignments = await _db.RolePermissionPackages.ToListAsync(cancellationToken);
        _db.RolePermissionPackages.RemoveRange(allAssignments);
        await _db.SaveChangesAsync(cancellationToken);

        var packagesBySlug = await _db.PermissionPackages.AsNoTracking()
            .ToDictionaryAsync(p => p.Slug, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var assign in payload.RolePackages)
        {
            var role = await _roleManager.FindByNameAsync(assign.RoleName);
            if (role is null || Roles.Canonical.Contains(assign.RoleName, StringComparer.OrdinalIgnoreCase))
                continue;
            if (!packagesBySlug.TryGetValue(assign.PackageSlug, out var package))
                continue;

            _db.RolePermissionPackages.Add(new RolePermissionPackage
            {
                Id = Guid.NewGuid(),
                RoleId = role.Id,
                PackageId = package.Id,
                AssignedAt = _time.GetUtcNow().UtcDateTime,
                AssignedByUserId = actorUserId,
            });
        }

        // Overrides: replace all with snapshot
        var overrides = await _db.UserPermissionOverrides.ToListAsync(cancellationToken);
        _db.UserPermissionOverrides.RemoveRange(overrides);
        foreach (var o in payload.Overrides)
        {
            _db.UserPermissionOverrides.Add(new UserPermissionOverride
            {
                Id = o.Id == Guid.Empty ? Guid.NewGuid() : o.Id,
                UserId = o.UserId,
                TenantId = o.TenantId,
                Permission = o.Permission,
                IsGranted = o.IsGranted,
                Reason = o.Reason,
                CreatedAt = o.CreatedAt == default ? _time.GetUtcNow().UtcDateTime : o.CreatedAt,
                CreatedByUserId = o.CreatedByUserId,
                ValidFrom = o.ValidFrom,
                ExpiresAt = o.ExpiresAt,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _audit.LogSystemOperationAsync(
                "PERMISSION_CONFIG_BACKUP_RESTORED",
                "PermissionConfigBackup",
                actorUserId ?? "system",
                Roles.SuperAdmin,
                description: $"Permission config restored from backup {backup.Id}",
                requestData: new { backup.Id, backup.PayloadHash },
                actionType: AuditEventType.PermissionConfigBackupRestored,
                entityId: backup.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to audit permission config restore");
        }

        return (true, null, null);
    }

    public async Task<PermissionConfigBackupSettingsDto> GetSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        var row = await EnsureSettingsAsync(cancellationToken);
        return new PermissionConfigBackupSettingsDto
        {
            AutoBackupBeforeChanges = row.AutoBackupBeforeChanges,
        };
    }

    public async Task<PermissionConfigBackupSettingsDto> SetSettingsAsync(
        PermissionConfigBackupSettingsDto settings,
        CancellationToken cancellationToken = default)
    {
        var row = await EnsureSettingsAsync(cancellationToken);
        row.AutoBackupBeforeChanges = settings.AutoBackupBeforeChanges;
        row.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);
        return new PermissionConfigBackupSettingsDto
        {
            AutoBackupBeforeChanges = row.AutoBackupBeforeChanges,
        };
    }

    public async Task TryAutoBackupBeforeChangeAsync(
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (!settings.AutoBackupBeforeChanges)
            return;

        lock (AutoBackupGate)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            if (_lastAutoBackupUtc.HasValue && now - _lastAutoBackupUtc.Value < AutoBackupDebounce)
                return;
            _lastAutoBackupUtc = now;
        }

        await CreateAsync(
            new CreatePermissionConfigBackupRequest
            {
                Name = $"Auto before change {_time.GetUtcNow().UtcDateTime:yyyy-MM-dd HH:mm} UTC",
            },
            actorUserId,
            PermissionConfigBackupTriggers.AutoBeforeChange,
            cancellationToken);
    }

    private async Task<PermissionConfigBackupSettings> EnsureSettingsAsync(CancellationToken cancellationToken)
    {
        var row = await _db.PermissionConfigBackupSettings.FirstOrDefaultAsync(s => s.Id == 1, cancellationToken);
        if (row is not null)
            return row;

        row = new PermissionConfigBackupSettings
        {
            Id = 1,
            AutoBackupBeforeChanges = true,
            UpdatedAt = _time.GetUtcNow().UtcDateTime,
        };
        _db.PermissionConfigBackupSettings.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return row;
    }

    private async Task EnforceRetentionAsync(CancellationToken cancellationToken)
    {
        var stale = await _db.PermissionConfigBackups
            .OrderByDescending(b => b.CreatedAt)
            .Skip(RetentionCount)
            .ToListAsync(cancellationToken);
        if (stale.Count == 0)
            return;
        _db.PermissionConfigBackups.RemoveRange(stale);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<PermissionConfigPayload> BuildPayloadAsync(CancellationToken cancellationToken)
    {
        var roles = await _roleManager.Roles.ToListAsync(cancellationToken);
        var customRoles = new List<RoleSnap>();
        foreach (var role in roles)
        {
            var name = role.Name ?? string.Empty;
            if (Roles.Canonical.Contains(name, StringComparer.OrdinalIgnoreCase))
                continue;

            var claims = await _roleManager.GetClaimsAsync(role);
            var perms = claims
                .Where(c => string.Equals(c.Type, PermissionCatalog.PermissionClaimType, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
            customRoles.Add(new RoleSnap { Name = name, Permissions = perms });
        }

        var packages = await _db.PermissionPackages.AsNoTracking()
            .Include(p => p.Keys)
            .ToListAsync(cancellationToken);

        var assignments = await _db.RolePermissionPackages.AsNoTracking()
            .Include(a => a.Package)
            .ToListAsync(cancellationToken);

        var roleIdToName = roles.ToDictionary(r => r.Id, r => r.Name ?? string.Empty);

        var overrides = await _db.UserPermissionOverrides.AsNoTracking().ToListAsync(cancellationToken);

        return new PermissionConfigPayload
        {
            CustomRoles = customRoles,
            Packages = packages.Select(p => new PackageSnap
            {
                Slug = p.Slug,
                Name = p.Name,
                Description = p.Description,
                IsSystem = p.IsSystem,
                Permissions = p.Keys.Select(k => k.Permission).OrderBy(x => x).ToList(),
            }).ToList(),
            RolePackages = assignments
                .Where(a => a.Package != null && roleIdToName.ContainsKey(a.RoleId))
                .Select(a => new RolePackageSnap
                {
                    RoleName = roleIdToName[a.RoleId],
                    PackageSlug = a.Package!.Slug,
                })
                .ToList(),
            Overrides = overrides.Select(o => new OverrideSnap
            {
                Id = o.Id,
                UserId = o.UserId,
                TenantId = o.TenantId,
                Permission = o.Permission,
                IsGranted = o.IsGranted,
                Reason = o.Reason,
                CreatedAt = o.CreatedAt,
                CreatedByUserId = o.CreatedByUserId,
                ValidFrom = o.ValidFrom,
                ExpiresAt = o.ExpiresAt,
            }).ToList(),
        };
    }

    private static bool TryDeserialize(string json, out PermissionConfigPayload? payload)
    {
        try
        {
            payload = JsonSerializer.Deserialize<PermissionConfigPayload>(json, JsonOptions);
            return payload is not null;
        }
        catch
        {
            payload = null;
            return false;
        }
    }

    private static PermissionConfigBackupListItemDto MapListItem(PermissionConfigBackup entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Note = entity.Note,
        CreatedAt = entity.CreatedAt,
        CreatedByUserId = entity.CreatedByUserId,
        Trigger = entity.Trigger,
        SchemaVersion = entity.SchemaVersion,
    };

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed class PermissionConfigPayload
    {
        public List<RoleSnap> CustomRoles { get; set; } = new();
        public List<PackageSnap> Packages { get; set; } = new();
        public List<RolePackageSnap> RolePackages { get; set; } = new();
        public List<OverrideSnap> Overrides { get; set; } = new();
    }

    private sealed class RoleSnap
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
    }

    private sealed class PackageSnap
    {
        public string Slug { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsSystem { get; set; }
        public List<string> Permissions { get; set; } = new();
    }

    private sealed class RolePackageSnap
    {
        public string RoleName { get; set; } = string.Empty;
        public string PackageSlug { get; set; } = string.Empty;
    }

    private sealed class OverrideSnap
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public Guid? TenantId { get; set; }
        public string Permission { get; set; } = string.Empty;
        public bool IsGranted { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedByUserId { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
