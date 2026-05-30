using KasseAPI_Final.Data;
using KasseAPI_Final.Data.CategorySeed;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface ICategoryDemoResetService
{
    Task<CategoryDemoResetResultDto> ResetDemoDisplayNamesAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed class CategoryDemoResetService : ICategoryDemoResetService
{
    private readonly AppDbContext _context;

    public CategoryDemoResetService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CategoryDemoResetResultDto> ResetDemoDisplayNamesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var categories = await _context.Categories
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var resetCount = 0;
        var skippedCount = 0;

        foreach (var category in categories)
        {
            var targetName = ResolveDemoDisplayName(category);
            if (targetName == null)
            {
                skippedCount++;
                continue;
            }

            if (string.Equals(category.Name, targetName, StringComparison.Ordinal))
            {
                skippedCount++;
                continue;
            }

            var oldName = category.Name;
            category.Name = targetName;
            category.UpdatedAt = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(category.OriginalDemoName))
                category.OriginalDemoName = targetName;

            await _context.Products
                .Where(p => p.TenantId == tenantId && p.CategoryId == category.Id && p.Category == oldName)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(p => p.Category, targetName),
                    cancellationToken)
                .ConfigureAwait(false);

            resetCount++;
        }

        if (resetCount > 0)
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CategoryDemoResetResultDto
        {
            ResetCount = resetCount,
            SkippedCount = skippedCount,
            TotalCategories = categories.Count,
        };
    }

    internal static string? ResolveDemoDisplayName(Category category)
    {
        if (!string.IsNullOrWhiteSpace(category.OriginalDemoName))
            return category.OriginalDemoName.Trim();

        if (SystemCategories.TryResolve(category.Key, out var seed))
            return seed.DisplayName;

        return null;
    }
}
