using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>Admin panel UI preferences for the signed-in user (theme, density, defaults).</summary>
[Authorize]
[ApiController]
[Route("api/admin/user/preferences")]
[Produces("application/json")]
public sealed class UserPreferencesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<UserPreferencesController> _logger;

    public UserPreferencesController(AppDbContext context, ILogger<UserPreferencesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(UserPreferencesResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserPreferencesResponseDto>> GetPreferences(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var row = await _context.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (row == null)
            return Ok(ToDto(null));

        return Ok(ToDto(row));
    }

    [HttpPut]
    [ProducesResponseType(typeof(UserPreferencesResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserPreferencesResponseDto>> SavePreferences(
        [FromBody] SaveUserPreferencesRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { message = "Request body is required." });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var themeMode = UserPreferencesNormalizer.NormalizeThemeMode(request.ThemeMode);
        var densityMode = UserPreferencesNormalizer.NormalizeDensityMode(request.DensityMode);
        var defaultPage = UserPreferencesNormalizer.NormalizeDefaultPage(request.DefaultPage);
        var dateFormat = UserPreferencesNormalizer.NormalizeDateFormat(request.DateFormat);
        var timeFormat = UserPreferencesNormalizer.NormalizeTimeFormat(request.TimeFormat);
        var reducedAnimations = request.ReducedAnimations ?? false;

        var row = await _context.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (row == null)
        {
            row = new UserPreferences
            {
                Id = Guid.NewGuid(),
                UserId = userId,
            };
            _context.UserPreferences.Add(row);
        }

        row.ThemeMode = themeMode;
        row.DensityMode = densityMode;
        row.DefaultPage = defaultPage;
        row.DateFormat = dateFormat;
        row.TimeFormat = timeFormat;
        row.ReducedAnimations = reducedAnimations;
        row.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "User preferences saved for user {UserId} (theme={Theme}, density={Density})",
            userId,
            themeMode,
            densityMode);

        return Ok(ToDto(row));
    }

    private static UserPreferencesResponseDto ToDto(UserPreferences? row) =>
        row == null
            ? new UserPreferencesResponseDto
            {
                ThemeMode = "system",
                DensityMode = "standard",
                DefaultPage = "/dashboard",
                DateFormat = "DD.MM.YYYY",
                TimeFormat = "24h",
                ReducedAnimations = false,
                UpdatedAtUtc = null,
            }
            : new UserPreferencesResponseDto
            {
                ThemeMode = row.ThemeMode,
                DensityMode = row.DensityMode,
                DefaultPage = row.DefaultPage,
                DateFormat = row.DateFormat,
                TimeFormat = row.TimeFormat,
                ReducedAnimations = row.ReducedAnimations,
                UpdatedAtUtc = row.UpdatedAtUtc,
            };
}
