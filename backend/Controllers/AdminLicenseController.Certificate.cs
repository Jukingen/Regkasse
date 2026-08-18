using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Services.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

public sealed partial class AdminLicenseController
{
    /// <summary>PDF certificate for the ambient mandant license (Manager self-service).</summary>
    [HttpGet("certificate")]
    [HasPermission(AppPermissions.LicenseManage)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadLicenseCertificate(CancellationToken cancellationToken = default)
    {
        var (tenantId, error) = await ResolveAccessibleMandantTenantIdAsync(null, cancellationToken)
            .ConfigureAwait(false);
        if (error != null)
            return error;

        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            return NotFound(new { message = "Tenant not found." });

        var (_, kind) = TenantLicenseStatusMapper.ComputeKindAndDays(
            tenant.LicenseValidUntilUtc,
            tenant.LicenseKey);

        var pdf = LicenseCertificatePdfGenerator.Generate(
            new LicenseCertificatePdfModel(
                tenant.Name,
                tenant.Slug,
                LicenseRenewalConfirmationEmailComposer.MaskLicenseKey(tenant.LicenseKey),
                kind,
                tenant.LicenseValidUntilUtc,
                DateTime.UtcNow));

        var fileName = $"license-certificate_{tenant.Slug}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        return File(pdf, "application/pdf", fileName);
    }
}
