using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Mandanten-Admin self-service license invoices (own tenant only).
/// Super Admin sales PDF remains <c>/api/admin/billing/license-sales/{id}/pdf</c>.
/// Not on <see cref="AdminBillingController"/> — that class is Super Admin
/// (<see cref="AppPermissions.SystemCritical"/>).
/// Permission: <see cref="AppPermissions.LicenseManage"/> (Mandanten-Admin).
/// There is no <c>billing.view</c> catalog key; Cashier <c>license.view</c> is not enough.
/// Missing ambient tenant or cross-tenant id → HTTP 404.
/// </summary>
[ApiController]
[Route("api/admin/billing/tenant-invoices")]
[Authorize]
[HasPermission(AppPermissions.LicenseManage)]
[Produces("application/json")]
public sealed class AdminTenantBillingInvoicesController : ControllerBase
{
    private readonly ITenantInvoiceService _invoices;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminTenantBillingInvoicesController(
        ITenantInvoiceService invoices,
        ICurrentTenantAccessor tenantAccessor)
    {
        _invoices = invoices;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(TenantInvoiceListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenantInvoices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetAmbientTenantId(out var tenantId))
            return NotFound();

        var result = await _invoices
            .GetInvoicesForTenantAsync(
                tenantId,
                page,
                pageSize,
                status,
                fromDate ?? fromUtc,
                toDate ?? toUtc,
                cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{id:guid}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenantInvoicePdf(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetAmbientTenantId(out var tenantId))
            return NotFound();

        try
        {
            var (pdf, fileName) = await _invoices
                .GetInvoicePdfForTenantAsync(tenantId, id, cancellationToken)
                .ConfigureAwait(false);
            return File(pdf, "application/pdf", fileName);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private bool TryGetAmbientTenantId(out Guid tenantId)
    {
        if (_tenantAccessor.TenantId is Guid id && id != Guid.Empty)
        {
            tenantId = id;
            return true;
        }

        tenantId = Guid.Empty;
        return false;
    }
}
