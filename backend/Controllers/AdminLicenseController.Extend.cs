using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.License;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

public sealed partial class AdminLicenseController
{
    /// <summary>
    /// Deprecated. Use <c>POST /api/license/activate</c> (unified server + tenant activation).
    /// </summary>
    [Obsolete("Use POST /api/license/activate.")]
    [HttpPost("activate")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(StatusCodes.Status308PermanentRedirect)]
    public IActionResult ActivateLicenseDeprecated([FromBody] ActivateLicenseRequest? body)
    {
        _logger.LogInformation(
            "Deprecated POST /api/admin/license/activate redirected to {Target}",
            UnifiedLicenseRoutes.Activate);
        return LicenseController.RedirectToUnifiedActivate();
    }

    /// <summary>
    /// Deprecated. Use <c>POST /api/license/activate</c> (billing extend is activation of the new sale key).
    /// </summary>
    [Obsolete("Use POST /api/license/activate.")]
    [HttpPost("extend")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(StatusCodes.Status308PermanentRedirect)]
    public IActionResult ExtendLicense([FromBody] ExtendLicenseRequest? request)
    {
        _logger.LogInformation(
            "Deprecated POST /api/admin/license/extend redirected to {Target}",
            UnifiedLicenseRoutes.Activate);
        return LicenseController.RedirectToUnifiedActivate();
    }
}
