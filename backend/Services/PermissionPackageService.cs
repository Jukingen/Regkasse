using System.Text.RegularExpressions;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class PermissionPackageService : IPermissionPackageService
{
    public const string NotFoundCode = "PACKAGE_NOT_FOUND";
    public const string SystemImmutableCode = "SYSTEM_PACKAGE_IMMUTABLE";
    public const string RoleNotFoundCode = "ROLE_NOT_FOUND";
    public const string SystemRoleCode = "SYSTEM_ROLE_NOT_EDITABLE";
    public const string InvalidPermissionsCode = "INVALID_PERMISSIONS";
    public const string AlreadyAssignedCode = "PACKAGE_ALREADY_ASSIGNED";
    public const string NotAssignedCode = "PACKAGE_NOT_ASSIGNED";

    private static readonly (string Slug, string Name, string Description, string[] Keys)[] SystemPackages =
    {
        (
            "reporting",
            "Reporting",
            "Reports and audit visibility",
            new[] { AppPermissions.ReportView, AppPermissions.ReportExport, AppPermissions.AuditView }),
        (
            "cash-operations",
            "Cash operations",
            "POS sales, payments, and register visibility",
            BuildCashOperationsKeys()),
        (
            "user-management",
            "User management",
            "Users and custom roles",
            new[]
            {
                AppPermissions.UserView,
                AppPermissions.UserManage,
                AppPermissions.RoleView,
                AppPermissions.RoleManage,
            }),
    };

    private readonly AppDbContext _db;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IRoleManagementService _roleManagement;
    private readonly IRolePermissionResolver _rolePermissionResolver;
    private readonly TimeProvider _time;
    private readonly ILogger<PermissionPackageService> _logger;
    private readonly SemaphoreSlim _seedLock = new(1, 1);

    public PermissionPackageService(
        AppDbContext db,
        RoleManager<IdentityRole> roleManager,
        IRoleManagementService roleManagement,
        IRolePermissionResolver rolePermissionResolver,
        TimeProvider time,
        ILogger<PermissionPackageService> logger)
    {
        _db = db;
        _roleManager = roleManager;
        _roleManagement = roleManagement;
        _rolePermissionResolver = rolePermissionResolver;
        _time = time;
        _logger = logger;
    }

    public async Task EnsureSeedAsync(CancellationToken cancellationToken = default)
    {
        await _seedLock.WaitAsync(cancellationToken);
        try
        {
            var now = _time.GetUtcNow().UtcDateTime;
            foreach (var def in SystemPackages)
            {
                var existing = await _db.PermissionPackages
                    .Include(p => p.Keys)
                    .FirstOrDefaultAsync(p => p.Slug == def.Slug, cancellationToken);

                if (existing is null)
                {
                    existing = new PermissionPackage
                    {
                        Id = Guid.NewGuid(),
                        Slug = def.Slug,
                        Name = def.Name,
                        Description = def.Description,
                        IsSystem = true,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    _db.PermissionPackages.Add(existing);
                    foreach (var key in def.Keys.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        _db.PermissionPackageKeys.Add(new PermissionPackageKey
                        {
                            Id = Guid.NewGuid(),
                            PackageId = existing.Id,
                            Permission = key,
                        });
                    }
                }
                else
                {
                    existing.Name = def.Name;
                    existing.Description = def.Description;
                    existing.IsSystem = true;
                    existing.UpdatedAt = now;

                    var desired = def.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var current = existing.Keys.Select(k => k.Permission).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var stale in existing.Keys.Where(k => !desired.Contains(k.Permission)).ToList())
                        _db.PermissionPackageKeys.Remove(stale);
                    foreach (var missing in desired.Where(k => !current.Contains(k)))
                    {
                        _db.PermissionPackageKeys.Add(new PermissionPackageKey
                        {
                            Id = Guid.NewGuid(),
                            PackageId = existing.Id,
                            Permission = missing,
                        });
                    }
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _seedLock.Release();
        }
    }

    public async Task<IReadOnlyList<PermissionPackageDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeedAsync(cancellationToken);
        var rows = await _db.PermissionPackages.AsNoTracking()
            .Include(p => p.Keys)
            .OrderByDescending(p => p.IsSystem)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<PermissionPackageDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSeedAsync(cancellationToken);
        var row = await _db.PermissionPackages.AsNoTracking()
            .Include(p => p.Keys)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<PermissionPackageDto?> CreateAsync(
        UpsertPermissionPackageRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var permissions = NormalizePermissions(request.Permissions);
        if (permissions is null)
            return null;

        var now = _time.GetUtcNow().UtcDateTime;
        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? Slugify(request.Name)
            : Slugify(request.Slug!);
        if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(request.Name))
            return null;

        if (await _db.PermissionPackages.AnyAsync(p => p.Slug == slug, cancellationToken))
            slug = $"{slug}-{Guid.NewGuid().ToString("N")[..6]}";

        var entity = new PermissionPackage
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsSystem = false,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUserId = actorUserId,
        };
        _db.PermissionPackages.Add(entity);
        foreach (var key in permissions)
        {
            _db.PermissionPackageKeys.Add(new PermissionPackageKey
            {
                Id = Guid.NewGuid(),
                PackageId = entity.Id,
                Permission = key,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(entity.Id, cancellationToken);
    }

    public async Task<PermissionPackageDto?> UpdateAsync(
        Guid id,
        UpsertPermissionPackageRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.PermissionPackages
            .Include(p => p.Keys)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity is null)
            return null;

        var permissions = NormalizePermissions(request.Permissions);
        if (permissions is null)
            return null;

        if (entity.IsSystem)
        {
            // System packages: allow description refresh only via EnsureSeed; reject mutation here.
            return null;
        }

        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.UpdatedAt = _time.GetUtcNow().UtcDateTime;

        _db.PermissionPackageKeys.RemoveRange(entity.Keys);
        entity.Keys.Clear();
        foreach (var key in permissions)
        {
            entity.Keys.Add(new PermissionPackageKey
            {
                Id = Guid.NewGuid(),
                PackageId = entity.Id,
                Permission = key,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(entity.Id, cancellationToken);
    }

    public async Task<(bool Succeeded, string? Code, string? Error)> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.PermissionPackages
            .Include(p => p.RoleAssignments)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity is null)
            return (false, NotFoundCode, "Package not found.");
        if (entity.IsSystem)
            return (false, SystemImmutableCode, "System packages cannot be deleted.");

        if (entity.RoleAssignments.Count > 0)
            _db.RolePermissionPackages.RemoveRange(entity.RoleAssignments);

        _db.PermissionPackages.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null, null);
    }

    public async Task<(bool Succeeded, string? Code, string? Error)> AddPackageToRoleAsync(
        string roleName,
        Guid packageId,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null)
            return (false, RoleNotFoundCode, "Role not found.");
        if (Roles.Canonical.Contains(roleName, StringComparer.OrdinalIgnoreCase))
            return (false, SystemRoleCode, "System roles cannot receive packages.");

        var package = await _db.PermissionPackages
            .Include(p => p.Keys)
            .FirstOrDefaultAsync(p => p.Id == packageId, cancellationToken);
        if (package is null)
            return (false, NotFoundCode, "Package not found.");

        var already = await _db.RolePermissionPackages.AnyAsync(
            a => a.RoleId == role.Id && a.PackageId == packageId,
            cancellationToken);
        if (already)
            return (false, AlreadyAssignedCode, "Package already assigned to role.");

        _db.RolePermissionPackages.Add(new RolePermissionPackage
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            PackageId = packageId,
            AssignedAt = _time.GetUtcNow().UtcDateTime,
            AssignedByUserId = actorUserId,
        });
        await _db.SaveChangesAsync(cancellationToken);

        var current = await _rolePermissionResolver.GetPermissionsForRolesAsync(new[] { roleName }, cancellationToken);
        var union = current
            .Concat(package.Keys.Select(k => k.Permission))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var setResult = await _roleManagement.SetRolePermissionsAsync(roleName, union, cancellationToken);
        if (setResult != SetRolePermissionsResult.Success)
        {
            _logger.LogWarning("Failed to union package permissions onto role {Role}: {Result}", roleName, setResult);
            return (false, setResult.ToString(), "Failed to update role permissions.");
        }

        return (true, null, null);
    }

    public async Task<(bool Succeeded, string? Code, string? Error)> RemovePackageFromRoleAsync(
        string roleName,
        Guid packageId,
        CancellationToken cancellationToken = default)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null)
            return (false, RoleNotFoundCode, "Role not found.");
        if (Roles.Canonical.Contains(roleName, StringComparer.OrdinalIgnoreCase))
            return (false, SystemRoleCode, "System roles cannot be edited via packages.");

        var assignment = await _db.RolePermissionPackages
            .FirstOrDefaultAsync(a => a.RoleId == role.Id && a.PackageId == packageId, cancellationToken);
        if (assignment is null)
            return (false, NotAssignedCode, "Package is not assigned to this role.");

        var removedPackage = await _db.PermissionPackages
            .Include(p => p.Keys)
            .FirstOrDefaultAsync(p => p.Id == packageId, cancellationToken);

        _db.RolePermissionPackages.Remove(assignment);
        await _db.SaveChangesAsync(cancellationToken);

        var remaining = await _db.RolePermissionPackages
            .AsNoTracking()
            .Where(a => a.RoleId == role.Id)
            .Include(a => a.Package!)
            .ThenInclude(p => p.Keys)
            .ToListAsync(cancellationToken);

        var remainingKeys = remaining
            .SelectMany(a => a.Package?.Keys ?? Enumerable.Empty<PermissionPackageKey>())
            .Select(k => k.Permission)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var current = await _rolePermissionResolver.GetPermissionsForRolesAsync(new[] { roleName }, cancellationToken);
        var removedKeys = (removedPackage?.Keys ?? Enumerable.Empty<PermissionPackageKey>())
            .Select(k => k.Permission)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var next = current
            .Where(p => !removedKeys.Contains(p) || remainingKeys.Contains(p))
            .Concat(remainingKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var setResult = await _roleManagement.SetRolePermissionsAsync(roleName, next, cancellationToken);
        if (setResult != SetRolePermissionsResult.Success)
            return (false, setResult.ToString(), "Failed to recompute role permissions.");

        return (true, null, null);
    }

    public async Task<IReadOnlyList<RoleAssignedPackageDto>> ListAssignedPackagesForRoleAsync(
        string roleName,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeedAsync(cancellationToken);
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null)
            return Array.Empty<RoleAssignedPackageDto>();

        var rows = await _db.RolePermissionPackages.AsNoTracking()
            .Where(a => a.RoleId == role.Id)
            .Include(a => a.Package!)
            .ThenInclude(p => p.Keys)
            .ToListAsync(cancellationToken);

        return rows
            .Where(a => a.Package != null)
            .Select(a => new RoleAssignedPackageDto
            {
                Id = a.Package!.Id,
                Slug = a.Package.Slug,
                Name = a.Package.Name,
                PermissionCount = a.Package.Keys.Count,
            })
            .OrderBy(p => p.Name)
            .ToList();
    }

    private static string[] BuildCashOperationsKeys()
    {
        var keys = new List<string>
        {
            AppPermissions.SaleView,
            AppPermissions.PaymentView,
            AppPermissions.CashRegisterView,
        };

        void AddIfPresent(string key)
        {
            if (PermissionCatalog.All.Contains(key, StringComparer.OrdinalIgnoreCase))
                keys.Add(key);
        }

        AddIfPresent(AppPermissions.CartView);
        AddIfPresent(AppPermissions.CartManage);
        AddIfPresent(AppPermissions.OrderView);
        AddIfPresent(AppPermissions.OrderCreate);
        AddIfPresent(AppPermissions.OrderUpdate);
        AddIfPresent(AppPermissions.OrderCancel);
        return keys.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static List<string>? NormalizePermissions(IReadOnlyList<string>? permissions)
    {
        if (permissions is null)
            return new List<string>();

        var list = permissions
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (list.Any(p => !PermissionCatalogMetadata.IsValidPermissionKey(p)))
            return null;

        return list;
    }

    private static string Slugify(string input)
    {
        var s = input.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"[^a-z0-9]+", "-");
        s = Regex.Replace(s, @"-+", "-").Trim('-');
        return s.Length > 64 ? s[..64] : s;
    }

    private static PermissionPackageDto Map(PermissionPackage entity)
    {
        var keys = entity.Keys
            .Select(k => k.Permission)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new PermissionPackageDto
        {
            Id = entity.Id,
            Slug = entity.Slug,
            Name = entity.Name,
            Description = entity.Description,
            IsSystem = entity.IsSystem,
            PermissionCount = keys.Count,
            Permissions = keys,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }
}
