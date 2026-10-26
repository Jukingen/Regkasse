using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DemoRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin inbox for marketing demo requests. Read-only.</summary>
[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/admin/demo-requests")]
[Produces("application/json")]
public sealed class AdminDemoRequestsController : ControllerBase
{
    private readonly IDemoRequestService _demoRequests;

    public AdminDemoRequestsController(IDemoRequestService demoRequests)
    {
        _demoRequests = demoRequests;
    }

    [HttpGet]
    [ProducesResponseType(typeof(DemoRequestListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!IsSuperAdmin(User))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                ApiError.Forbidden("Demo requests require SuperAdmin."));
        }

        var list = await _demoRequests.ListAsync(page, pageSize, cancellationToken).ConfigureAwait(false);
        return Ok(list);
    }

    private static bool IsSuperAdmin(ClaimsPrincipal user) =>
        user.IsInRole(Roles.SuperAdmin);
}
