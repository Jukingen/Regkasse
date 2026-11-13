using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Batch-loads user / tenant labels for backup run DTOs.</summary>
public sealed class BackupRunDisplayLookup
{
    public IReadOnlyDictionary<string, (string? Name, string? Email)> Users { get; }
    public IReadOnlyDictionary<Guid, (string Name, string Slug)> Tenants { get; }

    private BackupRunDisplayLookup(
        IReadOnlyDictionary<string, (string? Name, string? Email)> users,
        IReadOnlyDictionary<Guid, (string Name, string Slug)> tenants)
    {
        Users = users;
        Tenants = tenants;
    }

    public static async Task<BackupRunDisplayLookup> LoadAsync(
        AppDbContext db,
        IEnumerable<BackupRun> runs,
        CancellationToken cancellationToken = default)
    {
        var runList = runs as IList<BackupRun> ?? runs.ToList();
        var userIds = runList
            .Select(r => r.RequestedByUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var tenantIds = runList
            .Where(r => r.TenantId is Guid tid && tid != Guid.Empty)
            .Select(r => r.TenantId!.Value)
            .Distinct()
            .ToList();

        Dictionary<string, (string? Name, string? Email)> users = new(StringComparer.Ordinal);
        if (userIds.Count > 0)
        {
            var rows = await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName, u.Email })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var u in rows)
            {
                var name = $"{u.FirstName} {u.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(name))
                    name = u.UserName ?? string.Empty;
                users[u.Id] = (string.IsNullOrWhiteSpace(name) ? null : name, u.Email);
            }
        }

        Dictionary<Guid, (string Name, string Slug)> tenants = new();
        if (tenantIds.Count > 0)
        {
            var rows = await db.Tenants.AsNoTracking()
                .Where(t => tenantIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Name, t.Slug })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var t in rows)
                tenants[t.Id] = (t.Name, t.Slug);
        }

        return new BackupRunDisplayLookup(users, tenants);
    }

    public (string? Name, string? Email) UserFor(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return (null, null);
        return Users.TryGetValue(userId.Trim(), out var row) ? row : (null, null);
    }

    public (string? Name, string? Slug) TenantFor(Guid? tenantId)
    {
        if (tenantId is not Guid id || id == Guid.Empty)
            return (null, null);
        return Tenants.TryGetValue(id, out var row) ? row : (null, null);
    }
}
