using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Feedback;

public sealed class AdminFeedbackService : IAdminFeedbackService
{
    private readonly AppDbContext _db;

    public AdminFeedbackService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AdminFeedbackDto> CreateAsync(
        Guid tenantId,
        string userId,
        string? displayName,
        CreateAdminFeedbackRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var category = request.Category.Trim();
        if (!AdminFeedbackCategories.IsValid(category))
            throw new ArgumentException("Invalid feedback category.", nameof(request));

        var title = request.Title.Trim();
        var message = request.Message.Trim();
        if (title.Length < 3 || message.Length < 10)
            throw new ArgumentException("Title or message is too short.", nameof(request));

        if (request.Rating is < 1 or > 5)
            throw new ArgumentException("Rating must be between 1 and 5.", nameof(request));

        var now = DateTime.UtcNow;
        var entity = new AdminUserFeedback
        {
            TenantId = tenantId,
            Category = category,
            Status = AdminFeedbackStatuses.UnderReview,
            Title = title,
            Message = message,
            Rating = request.Rating,
            PagePath = SanitizePagePath(request.PagePath),
            SubmittedByUserId = userId,
            SubmittedByDisplayName = Truncate(displayName, 200),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _db.AdminUserFeedback.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(entity, tenantName: null);
    }

    public async Task<AdminFeedbackListResponseDto> ListMineAsync(
        string userId,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(0, offset);

        var query = _db.AdminUserFeedback.AsNoTracking()
            .Where(f => f.SubmittedByUserId == userId)
            .OrderByDescending(f => f.CreatedAtUtc);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query.Skip(offset).Take(limit).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new AdminFeedbackListResponseDto
        {
            Total = total,
            Items = rows.Select(r => Map(r, null)).ToList(),
        };
    }

    public async Task<AdminFeedbackListResponseDto> ListAllAsync(
        string? status,
        string? category,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        offset = Math.Max(0, offset);

        var query = _db.AdminUserFeedback.AsNoTracking()
            .IgnoreQueryFilters()
            .Include(f => f.Tenant)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && AdminFeedbackStatuses.IsValid(status))
            query = query.Where(f => f.Status == status.Trim());

        if (!string.IsNullOrWhiteSpace(category) && AdminFeedbackCategories.IsValid(category))
            query = query.Where(f => f.Category == category.Trim());

        query = query.OrderByDescending(f => f.CreatedAtUtc);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query.Skip(offset).Take(limit).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new AdminFeedbackListResponseDto
        {
            Total = total,
            Items = rows.Select(r => Map(r, r.Tenant?.Name)).ToList(),
        };
    }

    public async Task<AdminFeedbackDto?> UpdateStatusAsync(
        Guid id,
        string reviewerUserId,
        UpdateAdminFeedbackStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var status = request.Status.Trim();
        if (!AdminFeedbackStatuses.IsValid(status))
            throw new ArgumentException("Invalid feedback status.", nameof(request));

        var entity = await _db.AdminUserFeedback
            .IgnoreQueryFilters()
            .Include(f => f.Tenant)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            return null;

        entity.Status = status;
        entity.ReviewerNote = Truncate(request.ReviewerNote?.Trim(), 1000);
        entity.ReviewedByUserId = reviewerUserId;
        entity.ReviewedAtUtc = DateTime.UtcNow;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(entity, entity.Tenant?.Name);
    }

    private static AdminFeedbackDto Map(AdminUserFeedback entity, string? tenantName) =>
        new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            TenantName = tenantName,
            Category = entity.Category,
            Status = entity.Status,
            Title = entity.Title,
            Message = entity.Message,
            Rating = entity.Rating,
            PagePath = entity.PagePath,
            SubmittedByUserId = entity.SubmittedByUserId,
            SubmittedByDisplayName = entity.SubmittedByDisplayName,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            ReviewedByUserId = entity.ReviewedByUserId,
            ReviewedAtUtc = entity.ReviewedAtUtc,
            ReviewerNote = entity.ReviewerNote,
        };

    private static string? SanitizePagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        var trimmed = path.Trim();
        var noQuery = trimmed.Split('?', '#')[0];
        if (noQuery.Length == 0 || noQuery[0] != '/')
            return null;
        return Truncate(noQuery, 500);
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return value.Length <= max ? value : value[..max];
    }
}
