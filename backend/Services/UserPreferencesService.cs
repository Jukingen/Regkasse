using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IUserPreferencesService
{
    Task<UserPreferencesResponseDto> GetPreferencesAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserPreferencesResponseDto> UpdatePreferencesAsync(
        string userId,
        SaveUserPreferencesRequestDto request,
        CancellationToken cancellationToken = default);
}

public sealed class UserPreferencesService : IUserPreferencesService
{
    private readonly AppDbContext _db;
    private readonly ILogger<UserPreferencesService> _logger;

    public UserPreferencesService(AppDbContext db, ILogger<UserPreferencesService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<UserPreferencesResponseDto> GetPreferencesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        return ToDto(row);
    }

    public async Task<UserPreferencesResponseDto> UpdatePreferencesAsync(
        string userId,
        SaveUserPreferencesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var themeMode = UserPreferencesNormalizer.NormalizeThemeMode(request.ThemeMode);
        var densityMode = UserPreferencesNormalizer.NormalizeDensityMode(request.DensityMode);
        var defaultPage = UserPreferencesNormalizer.NormalizeDefaultPage(request.DefaultPage);
        var dateFormat = UserPreferencesNormalizer.NormalizeDateFormat(request.DateFormat);
        var timeFormat = UserPreferencesNormalizer.NormalizeTimeFormat(request.TimeFormat);
        var timeZone = UserPreferencesNormalizer.NormalizeTimeZone(request.TimeZone);
        var language = UserPreferencesNormalizer.NormalizeLanguage(request.Language);
        var reducedAnimations = request.ReducedAnimations ?? false;

        var row = await _db.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new UserPreferences
            {
                Id = Guid.NewGuid(),
                UserId = userId,
            };
            _db.UserPreferences.Add(row);
        }

        row.ThemeMode = themeMode;
        row.DensityMode = densityMode;
        row.DefaultPage = defaultPage;
        row.DateFormat = dateFormat;
        row.TimeFormat = timeFormat;
        row.TimeZone = timeZone;
        row.Language = language;
        row.ReducedAnimations = reducedAnimations;
        row.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "User preferences saved for user {UserId} (theme={Theme}, density={Density}, dateFormat={DateFormat}, timeZone={TimeZone})",
            userId,
            themeMode,
            densityMode,
            dateFormat,
            timeZone);

        return ToDto(row);
    }

    internal static UserPreferencesResponseDto ToDto(UserPreferences? row) =>
        row is null
            ? new UserPreferencesResponseDto
            {
                ThemeMode = "system",
                DensityMode = "standard",
                DefaultPage = "/dashboard",
                DateFormat = UserPreferencesNormalizer.DefaultDateFormat,
                TimeFormat = "24h",
                TimeZone = UserPreferencesNormalizer.DefaultTimeZone,
                Language = UserPreferencesNormalizer.DefaultLanguage,
                ReducedAnimations = false,
                UpdatedAtUtc = null,
            }
            : new UserPreferencesResponseDto
            {
                ThemeMode = row.ThemeMode,
                DensityMode = row.DensityMode,
                DefaultPage = row.DefaultPage,
                DateFormat = row.DateFormat ?? UserPreferencesNormalizer.DefaultDateFormat,
                TimeFormat = row.TimeFormat ?? "24h",
                TimeZone = row.TimeZone ?? UserPreferencesNormalizer.DefaultTimeZone,
                Language = row.Language ?? UserPreferencesNormalizer.DefaultLanguage,
                ReducedAnimations = row.ReducedAnimations,
                UpdatedAtUtc = row.UpdatedAtUtc,
            };
}
