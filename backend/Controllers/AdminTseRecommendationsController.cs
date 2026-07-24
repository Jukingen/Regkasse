using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE smart recommendations (diagnostic advisory workflow).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/recommendations")]
[Produces("application/json")]
public sealed class AdminTseRecommendationsController : ControllerBase
{
    private readonly ITseRecommendationService _recommendations;

    public AdminTseRecommendationsController(ITseRecommendationService recommendations)
    {
        _recommendations = recommendations;
    }

    [HttpGet]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TseRecommendationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TseRecommendationDto>>> GetRecommendations(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _recommendations
                .GetRecommendationsAsync(tenantId, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{recommendationId:guid}/apply")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseRecommendationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseRecommendationResultDto>> Apply(
        Guid recommendationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _recommendations
                .ApplyRecommendationAsync(recommendationId, User.GetActorUserId(), cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{recommendationId:guid}/dismiss")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseRecommendationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseRecommendationResultDto>> Dismiss(
        Guid recommendationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _recommendations
                .DismissRecommendationAsync(recommendationId, User.GetActorUserId(), cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{recommendationId:guid}/rate")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseRecommendationFeedbackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseRecommendationFeedbackDto>> Rate(
        Guid recommendationId,
        [FromBody] TseRecommendationRateRequestDto? body,
        CancellationToken cancellationToken = default)
    {
        body ??= new TseRecommendationRateRequestDto();
        try
        {
            return Ok(await _recommendations
                .RateRecommendationAsync(
                    recommendationId,
                    body.Rating,
                    User.GetActorUserId(),
                    cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
