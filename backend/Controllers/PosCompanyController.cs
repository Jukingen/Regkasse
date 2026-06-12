using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Read-only tenant company info for POS (RKSV §8 header). Admin mutations: <c>PUT /api/company/settings</c>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/pos/company")]
[Route("api/pos/company-profile")]
public sealed class PosCompanyController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public PosCompanyController(AppDbContext context, ICurrentTenantAccessor tenantAccessor)
    {
        _context = context;
        _tenantAccessor = tenantAccessor;
    }

    /// <summary>Returns RKSV header fields for receipt printing. Empty DTO when tenant has no saved settings row.</summary>
    [HttpGet]
    [HasPermission(AppPermissions.CartView)]
    [ProducesResponseType(typeof(PosCompanyInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PosCompanyInfoDto>> GetCompanyInfo(CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var settings = await _context.CompanySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (settings == null)
            return Ok(new PosCompanyInfoDto());

        return Ok(new PosCompanyInfoDto
        {
            CompanyName = settings.CompanyName,
            CompanyAddress = settings.CompanyAddress,
            TaxNumber = settings.CompanyTaxNumber,
            ReceiptFooter = string.IsNullOrWhiteSpace(settings.CompanyDescription)
                ? null
                : settings.CompanyDescription,
        });
    }
}
