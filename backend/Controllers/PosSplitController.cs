using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>POS bill split: start session, assign items to guests/seats, complete into separate carts.</summary>
[ApiController]
[Route("api/pos/split")]
[Authorize]
public sealed class PosSplitController : ControllerBase
{
    private readonly IPosSplitSessionService _splitSessions;

    public PosSplitController(IPosSplitSessionService splitSessions)
    {
        _splitSessions = splitSessions;
    }

    [HttpPost("start")]
    [ProducesResponseType(typeof(SplitSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SplitSessionDto>> StartSplit(
        [FromBody] StartSplitRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var session = await _splitSessions.StartSplitAsync(userId, request, cancellationToken);
            return Ok(session);
        }
        catch (PosSplitSessionException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/assign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignItem(
        Guid id,
        [FromBody] AssignItemRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            await _splitSessions.AssignItemAsync(userId, id, request, cancellationToken);
            return Ok();
        }
        catch (PosSplitSessionException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(List<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<List<Guid>>> CompleteSplit(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var newCartIds = await _splitSessions.CompleteSplitAsync(userId, id, cancellationToken);
            return Ok(newCartIds.ToList());
        }
        catch (PosSplitSessionException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }
}
