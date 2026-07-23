using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Admin panel UI preferences for the signed-in user (theme, density, date/time, locale).</summary>
[Authorize]
[ApiController]
[Route("api/admin/user/preferences")]
[Produces("application/json")]
public sealed class UserPreferencesController : ControllerBase
{
    private readonly IUserPreferencesService _preferences;

    public UserPreferencesController(IUserPreferencesService preferences)
    {
        _preferences = preferences;
    }

    [HttpGet]
    [ProducesResponseType(typeof(UserPreferencesResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserPreferencesResponseDto>> GetPreferences(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var dto = await _preferences.GetPreferencesAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(dto);
    }

    [HttpPut]
    [ProducesResponseType(typeof(UserPreferencesResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserPreferencesResponseDto>> SavePreferences(
        [FromBody] SaveUserPreferencesRequestDto? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { message = "Request body is required." });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var dto = await _preferences
            .UpdatePreferencesAsync(userId, request, cancellationToken)
            .ConfigureAwait(false);
        return Ok(dto);
    }
}
