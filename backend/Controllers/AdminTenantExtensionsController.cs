using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.Extensions;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Ambient-tenant extension list and enablement. Read: settings.view. Write: settings.manage.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tenant/extensions")]
[Produces("application/json")]
public sealed class AdminTenantExtensionsController : ControllerBase
{
    private readonly ITenantExtensionAdminService _extensions;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminTenantExtensionsController(
        ITenantExtensionAdminService extensions,
        ICurrentTenantAccessor tenantAccessor)
    {
        _extensions = extensions;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(IReadOnlyList<TenantExtensionStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TenantExtensionStatusDto>>> List(
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var list = await _extensions.ListAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(list);
    }

    [HttpPut]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(TenantExtensionStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantExtensionStatusDto>> Set(
        [FromBody] SetTenantExtensionRequest? body,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();
        if (body is null || string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { message = "Name is required.", code = "EXTENSION_NAME_REQUIRED" });

        var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var actorRole = User.FindFirstValue(ClaimTypes.Role) ?? "unknown";
        try
        {
            var status = await _extensions
                .SetEnabledAsync(tenantId, body.Name, body.Enabled, actorUserId, actorRole, cancellationToken)
                .ConfigureAwait(false);
            return Ok(status);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, code = "EXTENSION_UNKNOWN" });
        }
    }
}

public sealed class SetTenantExtensionRequest
{
    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; }
}
