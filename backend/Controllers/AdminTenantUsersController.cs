using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.AdminTenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Super-admin tenant user membership CRUD and invites.</summary>
[ApiController]
[Route("api/admin/tenants/{tenantId:guid}/users")]
[Authorize(Roles = Roles.SuperAdmin)]
[Produces("application/json")]
public sealed class AdminTenantUsersController : ControllerBase
{
    private readonly ITenantUserService _tenantUserService;
    private readonly ILogger<AdminTenantUsersController> _logger;

    public AdminTenantUsersController(ITenantUserService tenantUserService, ILogger<AdminTenantUsersController> logger)
    {
        _tenantUserService = tenantUserService;
        _logger = logger;
    }

    /// <summary>List users assigned to the tenant (active memberships).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TenantUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TenantUserDto>>> List(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var items = await _tenantUserService.ListAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (items == null)
            return NotFound(new { message = "Tenant not found." });
        return Ok(items);
    }

    /// <summary>Assign an existing user to the tenant with role and optional owner flag.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TenantUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantUserDto>> Add(
        Guid tenantId,
        [FromBody] AddAdminTenantUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var (result, error) = await _tenantUserService.AddAsync(tenantId, request, cancellationToken).ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (error != null)
            return BadRequest(new { message = error });

        return CreatedAtAction(nameof(List), new { tenantId }, result);
    }

    /// <summary>Invite user by email (creates account if needed, sends invitation email when SMTP is configured).</summary>
    [HttpPost("invite")]
    [ProducesResponseType(typeof(TenantUserInviteResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantUserInviteResultDto>> Invite(
        Guid tenantId,
        [FromBody] InviteTenantUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var (result, error) = await _tenantUserService.InviteAsync(tenantId, request, cancellationToken).ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (error != null)
            return BadRequest(new { message = error });

        _logger.LogInformation(
            "Tenant invite processed for {TenantId}: {Email}, created={Created}, emailSent={Sent}",
            tenantId,
            request.Email,
            result!.UserCreated,
            result.InvitationEmailSent);

        return CreatedAtAction(nameof(List), new { tenantId }, result);
    }

    /// <summary>Update role for a tenant user.</summary>
    [HttpPut("{userId}/role")]
    [ProducesResponseType(typeof(TenantUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantUserDto>> UpdateRole(
        Guid tenantId,
        string userId,
        [FromBody] UpdateTenantUserRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var (result, error) = await _tenantUserService.UpdateRoleAsync(tenantId, userId, request, cancellationToken)
            .ConfigureAwait(false);
        if (error is "Tenant not found." or "User not found." or "User is not assigned to this tenant.")
            return NotFound(new { message = error });
        if (error != null)
            return BadRequest(new { message = error });
        return Ok(result);
    }

    /// <summary>Generate a new password for a tenant user (shown once in the response).</summary>
    [HttpPost("{userId}/reset-password")]
    [ProducesResponseType(typeof(TenantUserPasswordResetResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantUserPasswordResetResultDto>> ResetPassword(
        Guid tenantId,
        string userId,
        [FromBody] ResetTenantUserPasswordRequest? request,
        CancellationToken cancellationToken = default)
    {
        var (result, error) = await _tenantUserService
            .ResetPasswordAsync(tenantId, userId, request, cancellationToken)
            .ConfigureAwait(false);
        if (error is "Tenant not found." or "User not found." or "User is not assigned to this tenant.")
            return NotFound(new { message = error });
        if (error != null)
            return BadRequest(new { message = error });

        _logger.LogInformation("Password reset for user {UserId} on tenant {TenantId}", userId, tenantId);
        return Ok(result);
    }

    /// <summary>Update tenant membership (role and/or owner flag).</summary>
    [HttpPut("{userId}")]
    [ProducesResponseType(typeof(TenantUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantUserDto>> Update(
        Guid tenantId,
        string userId,
        [FromBody] UpdateAdminTenantUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var (result, error) = await _tenantUserService.UpdateAsync(tenantId, userId, request, cancellationToken).ConfigureAwait(false);
        if (error is "Tenant not found." or "User not found." or "User is not assigned to this tenant.")
            return NotFound(new { message = error });
        if (error != null)
            return BadRequest(new { message = error });
        return Ok(result);
    }

    /// <summary>Remove user from tenant (deactivates membership).</summary>
    [HttpDelete("{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(
        Guid tenantId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var (success, error) = await _tenantUserService.RemoveAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        if (error is "Tenant not found." or "User is not assigned to this tenant.")
            return NotFound(new { message = error });
        if (!success)
            return BadRequest(new { message = error });
        return NoContent();
    }
}
