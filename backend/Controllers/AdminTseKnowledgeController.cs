using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE knowledge base / FAQ (operational docs only).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/knowledge")]
[Produces("application/json")]
public sealed class AdminTseKnowledgeController : ControllerBase
{
    private readonly ITseKnowledgeBaseService _knowledge;

    public AdminTseKnowledgeController(ITseKnowledgeBaseService knowledge)
    {
        _knowledge = knowledge;
    }

    [HttpGet("search")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TseKnowledgeArticleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TseKnowledgeArticleDto>>> Search(
        [FromQuery] string? q,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _knowledge.SearchArticlesAsync(q, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("popular")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TseKnowledgeArticleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TseKnowledgeArticleDto>>> Popular(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _knowledge.GetPopularArticlesAsync(limit, cancellationToken)
            .ConfigureAwait(false));
    }

    [HttpGet("faq")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TseKnowledgeArticleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TseKnowledgeArticleDto>>> Faq(
        CancellationToken cancellationToken = default)
    {
        return Ok(await _knowledge.GetFaqArticlesAsync(cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("{articleId:guid}")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseKnowledgeArticleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseKnowledgeArticleDto>> Get(
        Guid articleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _knowledge.GetArticleAsync(articleId, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{articleId:guid}/feedback")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseKnowledgeArticleFeedbackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseKnowledgeArticleFeedbackDto>> Feedback(
        Guid articleId,
        [FromBody] SubmitTseKnowledgeFeedbackRequestDto? body,
        CancellationToken cancellationToken = default)
    {
        body ??= new SubmitTseKnowledgeFeedbackRequestDto();
        try
        {
            return Ok(await _knowledge
                .SubmitFeedbackAsync(articleId, body.Rating, User.GetActorUserId(), cancellationToken)
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
