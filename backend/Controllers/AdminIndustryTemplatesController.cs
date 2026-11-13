using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.AdminTenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/industry-templates")]
[Produces("application/json")]
public sealed class AdminIndustryTemplatesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IIndustryTemplateStarterSeeder _starterSeeder;

    public AdminIndustryTemplatesController(AppDbContext db, IIndustryTemplateStarterSeeder starterSeeder)
    {
        _db = db;
        _starterSeeder = starterSeeder;
    }

    [HttpGet]
    [HasPermission(AppPermissions.RoleView)]
    [ProducesResponseType(typeof(IReadOnlyList<IndustryTemplateDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<IndustryTemplateDto>> List()
    {
        return Ok(IndustryPermissionTemplates.All.Select(Map).ToList());
    }

    [HttpGet("{id}")]
    [HasPermission(AppPermissions.RoleView)]
    [ProducesResponseType(typeof(IndustryTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IndustryTemplateDto> Get(string id)
    {
        var template = IndustryPermissionTemplates.Get(id);
        return template is null ? NotFound() : Ok(Map(template));
    }

    [HttpGet("tenants/{tenantId:guid}")]
    [HasPermission(AppPermissions.RoleView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> GetTenantTemplate(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
            return NotFound();

        var template = IndustryPermissionTemplates.Get(tenant.IndustryTemplateId);
        return Ok(new
        {
            tenantId,
            industryTemplateId = tenant.IndustryTemplateId,
            template = template is null ? null : Map(template),
        });
    }

    [HttpPut("tenants/{tenantId:guid}")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetTenantTemplate(
        Guid tenantId,
        [FromBody] SetTenantIndustryTemplateRequest body,
        CancellationToken cancellationToken)
    {
        if (!IndustryPermissionTemplates.IsValidId(body.IndustryTemplateId))
            return BadRequest(new { code = "INVALID_TEMPLATE", error = "Unknown industry template id." });

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
            return NotFound();

        var id = string.IsNullOrWhiteSpace(body.IndustryTemplateId)
                 || string.Equals(body.IndustryTemplateId, IndustryPermissionTemplates.None, StringComparison.OrdinalIgnoreCase)
            ? null
            : body.IndustryTemplateId.Trim();

        tenant.IndustryTemplateId = id;
        await _db.SaveChangesAsync(cancellationToken);

        var seeded = 0;
        if (body.SeedMissingStarters && id != null)
            seeded = await _starterSeeder.SeedMissingStartersAsync(tenantId, cancellationToken);

        return Ok(new { industryTemplateId = id, startersCreated = seeded });
    }

    private static IndustryTemplateDto Map(IndustryPermissionTemplates.Template t) => new()
    {
        Id = t.Id,
        Name = t.NameDe,
        Description = t.DescriptionDe,
        SuggestedDemoImportProfileId = t.SuggestedDemoImportProfileId,
        Slots = t.Slots.Select(s => new IndustryTemplateSlotDto
        {
            Key = s.Key,
            DisplayName = s.DisplayNameDe,
            SystemRole = s.SystemRole,
            RecommendedPackageSlugs = s.RecommendedPackageSlugs,
            SeedStarterUser = s.SeedStarterUser,
        }).ToList(),
    };
}
