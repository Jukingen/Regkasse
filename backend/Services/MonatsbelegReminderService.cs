using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <inheritdoc />
public sealed class MonatsbelegReminderService : IMonatsbelegReminderService
{
    private const string LevelGreen = "green";
    private const string LevelYellow = "yellow";
    private const string LevelRed = "red";

    private readonly AppDbContext _db;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly IRksvMonatsbelegPolicy _monatsbelegPolicy;

    public MonatsbelegReminderService(
        AppDbContext db,
        ISettingsTenantResolver tenantResolver,
        IRksvMonatsbelegPolicy monatsbelegPolicy)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _monatsbelegPolicy = monatsbelegPolicy;
    }

    /// <inheritdoc />
    public async Task<MonatsbelegStatusDto?> GetMonatsbelegStatusAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var register = await _db.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId && r.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (register == null)
            return null;

        var viennaNowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PostgreSqlUtcDateTime.AustriaTimeZone);
        var (viennaYear, viennaMonth) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        var today = viennaNowLocal.Date;

        var lastDayCurrent = DateTime.DaysInMonth(viennaYear, viennaMonth);
        var endOfCurrentMonth = new DateTime(viennaYear, viennaMonth, lastDayCurrent);
        var daysUntilDeadline = (endOfCurrentMonth - today).Days;

        var prevMonthAnchor = new DateTime(viennaYear, viennaMonth, 1).AddMonths(-1);
        var prevYear = prevMonthAnchor.Year;
        var prevMonth = prevMonthAnchor.Month;

        var hasCurrentMonth = await _monatsbelegPolicy
            .HasMonatsbelegForRegisterMonthAsync(cashRegisterId, viennaYear, viennaMonth, cancellationToken)
            .ConfigureAwait(false);
        var hasPreviousMonth = await _monatsbelegPolicy
            .HasMonatsbelegForRegisterMonthAsync(cashRegisterId, prevYear, prevMonth, cancellationToken)
            .ConfigureAwait(false);

        var isRequired = !hasCurrentMonth || !hasPreviousMonth;

        string warningLevel;
        if (!hasPreviousMonth)
        {
            // Previous Vienna month closed without a Monatsbeleg / Jahresbeleg substitute (December).
            warningLevel = LevelRed;
        }
        else if (hasCurrentMonth)
        {
            warningLevel = LevelGreen;
        }
        else if (daysUntilDeadline <= 1)
        {
            warningLevel = LevelRed;
        }
        else if (daysUntilDeadline <= 3)
        {
            warningLevel = LevelYellow;
        }
        else
        {
            warningLevel = LevelGreen;
        }

        return new MonatsbelegStatusDto
        {
            IsRequired = isRequired,
            DaysUntilDeadline = daysUntilDeadline,
            LastMonatsbelegDate = register.LastMonatsbelegUtc,
            WarningLevel = warningLevel,
        };
    }
}
