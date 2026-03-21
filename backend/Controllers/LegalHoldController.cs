using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Controllers;

/// <summary>Sprint 5: Legal hold API. Audit cleanup skips logs whose date falls within an active hold.</summary>
[Authorize]
[ApiController]
[Route("api/admin/legal-hold")]
[Produces("application/json")]
public class LegalHoldController : ControllerBase
{
    private readonly ILegalHoldService _legalHoldService;
    private readonly ILogger<LegalHoldController> _logger;

    public LegalHoldController(ILegalHoldService legalHoldService, ILogger<LegalHoldController> logger)
    {
        _legalHoldService = legalHoldService;
        _logger = logger;
    }

    /// <summary>POST: Create a legal hold for the given date range.</summary>
    [HttpPost]
    [HasPermission(AppPermissions.SystemCritical)]
    public async Task<ActionResult<LegalHoldDto>> Create([FromBody] CreateLegalHoldRequest request)
    {
        if (request.FromDate > request.ToDate)
            return BadRequest(new { error = "FromDate must be on or before ToDate." });
        var userId = User.GetActorUserId();
        var hold = await _legalHoldService.CreateAsync(request.FromDate, request.ToDate, request.Reason, userId);
        return CreatedAtAction(nameof(GetById), new { id = hold.Id }, ToDto(hold));
    }

    /// <summary>GET: List legal holds (active only by default).</summary>
    [HttpGet]
    [HasPermission(AppPermissions.AuditView)]
    public async Task<ActionResult<IEnumerable<LegalHoldDto>>> List([FromQuery] bool activeOnly = true)
    {
        var holds = await _legalHoldService.GetAllAsync(activeOnly);
        return Ok(holds.Select(ToDto));
    }

    /// <summary>GET: Get a single legal hold by id.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.AuditView)]
    public async Task<ActionResult<LegalHoldDto>> GetById(Guid id)
    {
        var hold = await _legalHoldService.GetByIdAsync(id);
        if (hold == null)
            return NotFound();
        return Ok(ToDto(hold));
    }

    /// <summary>DELETE: Lift (deactivate) a legal hold.</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(AppPermissions.SystemCritical)]
    public async Task<ActionResult> Deactivate(Guid id)
    {
        var ok = await _legalHoldService.DeactivateAsync(id);
        if (!ok)
            return NotFound();
        return NoContent();
    }

    private static LegalHoldDto ToDto(LegalHold h) => new()
    {
        Id = h.Id,
        FromDate = h.FromDate,
        ToDate = h.ToDate,
        Reason = h.Reason,
        IsActive = h.IsActive,
        CreatedAt = h.CreatedAt,
        CreatedBy = h.CreatedBy
    };
}

public class CreateLegalHoldRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? Reason { get; set; }
}

public class LegalHoldDto
{
    public Guid Id { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? Reason { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
