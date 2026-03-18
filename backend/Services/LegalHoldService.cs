using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>Sprint 5: Legal hold CRUD; used by cleanup to exclude held date ranges from deletion.</summary>
public class LegalHoldService : ILegalHoldService
{
    private readonly AppDbContext _context;
    private readonly ILogger<LegalHoldService> _logger;

    public LegalHoldService(AppDbContext context, ILogger<LegalHoldService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<LegalHold> CreateAsync(DateTime fromDate, DateTime toDate, string? reason, string? createdBy)
    {
        var hold = new LegalHold
        {
            FromDate = fromDate.Date,
            ToDate = toDate.Date,
            Reason = reason,
            IsActive = true,
            CreatedBy = createdBy
        };
        _context.LegalHolds.Add(hold);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Legal hold created: Id={Id}, From={From}, To={To}, Reason={Reason}", hold.Id, hold.FromDate, hold.ToDate, hold.Reason);
        return hold;
    }

    public async Task<IEnumerable<LegalHold>> GetActiveAsync()
    {
        return await _context.LegalHolds.AsNoTracking().Where(h => h.IsActive).OrderBy(h => h.FromDate).ToListAsync();
    }

    public async Task<IEnumerable<LegalHold>> GetAllAsync(bool activeOnly = true)
    {
        var query = _context.LegalHolds.AsNoTracking();
        if (activeOnly)
            query = query.Where(h => h.IsActive);
        return await query.OrderByDescending(h => h.CreatedAt).ToListAsync();
    }

    public async Task<LegalHold?> GetByIdAsync(Guid id)
    {
        return await _context.LegalHolds.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id);
    }

    public async Task<bool> DeactivateAsync(Guid id)
    {
        var hold = await _context.LegalHolds.FirstOrDefaultAsync(h => h.Id == id);
        if (hold == null)
            return false;
        hold.IsActive = false;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Legal hold deactivated: Id={Id}, From={From}, To={To}", hold.Id, hold.FromDate, hold.ToDate);
        return true;
    }
}
