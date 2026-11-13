using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Extensions;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/extensions/hair-salon-campaigns")]
[Produces("application/json")]
[HasPermission(AppPermissions.SettingsView)]
public sealed class AdminHairSalonCampaignsController : ControllerBase
{
    public const string NotEnabledCode = "EXTENSION_NOT_ENABLED";

    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ITenantExtensionRegistry _registry;

    public AdminHairSalonCampaignsController(
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        ITenantExtensionRegistry registry)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _registry = registry;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<HairSalonCampaignDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<HairSalonCampaignDto>>> List(CancellationToken cancellationToken)
    {
        var gate = Gate();
        if (gate is not null)
            return gate;

        var tenantId = _tenantAccessor.TenantId!.Value;
        var rows = await _db.HairSalonCampaigns
            .AsNoTracking()
            .Where(row => row.TenantId == tenantId)
            .OrderBy(row => row.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(rows.Select(ToDto).ToList());
    }

    [HttpPost]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(HairSalonCampaignDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HairSalonCampaignDto>> Create(
        [FromBody] UpsertHairSalonCampaignRequest request,
        CancellationToken cancellationToken)
    {
        var gate = Gate();
        if (gate is not null)
            return gate;
        if (!TryNormalize(request, out var normalized, out var error))
            return BadRequest(new { code = "CAMPAIGN_INVALID", message = error });

        var now = DateTime.UtcNow;
        var row = new HairSalonCampaign
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantAccessor.TenantId!.Value,
            Name = normalized.Name,
            DiscountPercent = normalized.DiscountPercent,
            AppliesToProductIds = normalized.ProductIds,
            ValidFromUtc = normalized.ValidFromUtc,
            ValidUntilUtc = normalized.ValidUntilUtc,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        _db.HairSalonCampaigns.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(List), ToDto(row));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(HairSalonCampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HairSalonCampaignDto>> Update(
        Guid id,
        [FromBody] UpsertHairSalonCampaignRequest request,
        CancellationToken cancellationToken)
    {
        var gate = Gate();
        if (gate is not null)
            return gate;
        if (!TryNormalize(request, out var normalized, out var error))
            return BadRequest(new { code = "CAMPAIGN_INVALID", message = error });

        var tenantId = _tenantAccessor.TenantId!.Value;
        var row = await _db.HairSalonCampaigns
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return NotFound();

        row.Name = normalized.Name;
        row.DiscountPercent = normalized.DiscountPercent;
        row.AppliesToProductIds = normalized.ProductIds;
        row.ValidFromUtc = normalized.ValidFromUtc;
        row.ValidUntilUtc = normalized.ValidUntilUtc;
        if (request.IsActive is bool active)
            row.IsActive = active;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(ToDto(row));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var gate = Gate();
        if (gate is not null)
            return gate;

        var tenantId = _tenantAccessor.TenantId!.Value;
        var row = await _db.HairSalonCampaigns
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return NotFound();

        row.IsActive = false;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private ActionResult? Gate()
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var enabled = _registry
            .GetForHook(tenantId, TenantExtensionHooks.PaymentBeforeSign)
            .Any(ext => string.Equals(ext.FeatureName, TenantExtensionNames.HairSalonCampaign, StringComparison.Ordinal));
        if (!enabled)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { code = NotEnabledCode, message = "The hair salon campaign extension is not enabled for this tenant." });
        }

        return null;
    }

    private static bool TryNormalize(
        UpsertHairSalonCampaignRequest? request,
        out NormalizedCampaign normalized,
        out string error)
    {
        normalized = default;
        error = string.Empty;
        if (request is null)
        {
            error = "Request body is required.";
            return false;
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 255)
        {
            error = "Name is required and must be at most 255 characters.";
            return false;
        }

        if (request.DiscountPercent < 0 || request.DiscountPercent > 100)
        {
            error = "Discount percent must be between 0 and 100.";
            return false;
        }

        var from = DateTime.SpecifyKind(request.ValidFromUtc, DateTimeKind.Utc);
        var until = DateTime.SpecifyKind(request.ValidUntilUtc, DateTimeKind.Utc);
        if (from >= until)
        {
            error = "validFromUtc must be earlier than validUntilUtc.";
            return false;
        }

        var productIds = (request.AppliesToProductIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        normalized = new NormalizedCampaign(
            name,
            decimal.Round(request.DiscountPercent, 2, MidpointRounding.AwayFromZero),
            productIds,
            from,
            until);
        return true;
    }

    private static HairSalonCampaignDto ToDto(HairSalonCampaign row) =>
        new(
            row.Id,
            row.Name,
            row.DiscountPercent,
            row.AppliesToProductIds ?? [],
            row.ValidFromUtc,
            row.ValidUntilUtc,
            row.IsActive,
            row.CreatedAtUtc,
            row.UpdatedAtUtc);

    private readonly record struct NormalizedCampaign(
        string Name,
        decimal DiscountPercent,
        List<Guid> ProductIds,
        DateTime ValidFromUtc,
        DateTime ValidUntilUtc);
}

public sealed record HairSalonCampaignDto(
    Guid Id,
    string Name,
    decimal DiscountPercent,
    IReadOnlyList<Guid> AppliesToProductIds,
    DateTime ValidFromUtc,
    DateTime ValidUntilUtc,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed class UpsertHairSalonCampaignRequest
{
    public string? Name { get; set; }

    public decimal DiscountPercent { get; set; }

    public List<Guid>? AppliesToProductIds { get; set; }

    public DateTime ValidFromUtc { get; set; }

    public DateTime ValidUntilUtc { get; set; }

    /// <summary>When set on update, replaces <c>is_active</c>. Create always stores active.</summary>
    public bool? IsActive { get; set; }
}
