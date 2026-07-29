using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.License;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

public sealed partial class AdminLicenseController
{
    /// <summary>
    /// Synthetic HTML/plain preview of the mandant license reminder email (no SMTP send).
    /// </summary>
    [HttpGet("reminder-email-preview")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(LicenseReminderEmailPreviewDto), StatusCodes.Status200OK)]
    public ActionResult<LicenseReminderEmailPreviewDto> GetReminderEmailPreview(
        [FromQuery] int daysUntilExpiry = 7,
        [FromQuery] string? tenantName = null,
        [FromQuery] string? adminName = null,
        [FromQuery] DateTime? expiryDate = null,
        [FromServices] IOptions<LicenseOptions> licenseOptions = null!,
        [FromServices] IOptions<EmailSmtpOptions> smtpOptions = null!)
    {
        var renewUrl = string.IsNullOrWhiteSpace(licenseOptions.Value.AdminLicenseUrl)
            ? LicenseReminderEmailComposer.DefaultAdminLicenseUrl
            : licenseOptions.Value.AdminLicenseUrl.Trim();
        var support = string.IsNullOrWhiteSpace(smtpOptions.Value.SupportContact)
            ? LicenseReminderEmailComposer.DefaultSupportEmail
            : smtpOptions.Value.SupportContact.Trim();

        var model = LicenseReminderEmailComposer.CreateSample(
            daysUntilExpiry,
            tenantName,
            adminName,
            expiryDate,
            renewUrl,
            support);
        var content = LicenseReminderEmailComposer.Build(model);
        var sampleExpiry = model.ExpiryDateUtc ?? DateTime.UtcNow.Date;

        return Ok(new LicenseReminderEmailPreviewDto(
            content.Subject,
            content.HtmlBody,
            content.PlainBody,
            model.DaysUntilExpiry,
            DateTime.SpecifyKind(sampleExpiry, DateTimeKind.Utc)));
    }
}
