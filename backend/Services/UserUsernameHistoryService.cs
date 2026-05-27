using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class UserUsernameHistoryService : IUserUsernameHistoryService
{
    private readonly AppDbContext _db;

    public UserUsernameHistoryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task RecordChangeAsync(
        string userId,
        string? oldUsername,
        string newUsername,
        string? changedByUserId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        _db.UserUsernameHistories.Add(new UserUsernameHistory
        {
            UserId = userId,
            OldUsername = oldUsername,
            NewUsername = newUsername,
            ChangedByUserId = changedByUserId,
            ChangedAtUtc = DateTime.UtcNow,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserUsernameHistoryDto>> ListForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await (
            from h in _db.UserUsernameHistories.AsNoTracking()
            join actor in _db.Users.AsNoTracking() on h.ChangedByUserId equals actor.Id into actors
            from actor in actors.DefaultIfEmpty()
            where h.UserId == userId
            orderby h.ChangedAtUtc descending
            select new UserUsernameHistoryDto
            {
                Id = h.Id,
                OldUsername = h.OldUsername,
                NewUsername = h.NewUsername,
                ChangedByUserId = h.ChangedByUserId,
                ChangedByEmail = actor != null ? actor.Email : null,
                ChangedAtUtc = h.ChangedAtUtc,
                Reason = h.Reason,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetKnownUsernamesForUserAsync(
        string userId,
        string? currentUsername,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.UserUsernameHistories
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .Select(h => new { h.OldUsername, h.NewUsername })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(currentUsername))
            names.Add(currentUsername.Trim());

        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(row.OldUsername))
                names.Add(row.OldUsername.Trim());
            if (!string.IsNullOrWhiteSpace(row.NewUsername))
                names.Add(row.NewUsername.Trim());
        }

        return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
