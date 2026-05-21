using System.Security.Claims;

using KasseAPI_Final.Authorization;

using KasseAPI_Final.Services.AdminTenants;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;



namespace KasseAPI_Final.Controllers;



/// <summary>Super-admin tenant user membership CRUD and creation.</summary>

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



    private string ActorUserId =>

        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";



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



    /// <summary>Create a new tenant user by email (generated password returned once; no email).</summary>

    [HttpPost]

    [ProducesResponseType(typeof(CreateTenantUserResultDto), StatusCodes.Status201Created)]

    [ProducesResponseType(StatusCodes.Status400BadRequest)]

    [ProducesResponseType(StatusCodes.Status404NotFound)]

    public async Task<ActionResult<CreateTenantUserResultDto>> Create(

        Guid tenantId,

        [FromBody] CreateTenantUserRequest request,

        CancellationToken cancellationToken = default)

    {

        if (!ModelState.IsValid)

            return ValidationProblem(ModelState);



        var (result, error) = await _tenantUserService

            .CreateAsync(tenantId, request, ActorUserId, cancellationToken)

            .ConfigureAwait(false);

        if (error == "Tenant not found.")

            return NotFound(new { message = error });

        if (error != null)

            return BadRequest(new { message = error });



        _logger.LogInformation(

            "Tenant user created for {TenantId}: {Email} by {Actor}",

            tenantId,

            request.Email,

            ActorUserId);



        return CreatedAtAction(nameof(List), new { tenantId }, result);

    }



    /// <summary>Quick-create a tenant user (auto email + password; audit logged).</summary>

    [HttpPost("quick")]

    [ProducesResponseType(typeof(CreateTenantUserResultDto), StatusCodes.Status201Created)]

    [ProducesResponseType(StatusCodes.Status400BadRequest)]

    [ProducesResponseType(StatusCodes.Status404NotFound)]

    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]

    public async Task<ActionResult<CreateTenantUserResultDto>> CreateQuick(

        Guid tenantId,

        [FromBody] CreateQuickTenantUserRequest request,

        CancellationToken cancellationToken = default)

    {

        if (!ModelState.IsValid)

            return ValidationProblem(ModelState);



        var (result, error) = await _tenantUserService

            .CreateQuickAsync(tenantId, request, ActorUserId, cancellationToken)

            .ConfigureAwait(false);

        if (error == "Tenant not found.")

            return NotFound(new { message = error });

        if (error != null && error.Contains("rate limit", StringComparison.OrdinalIgnoreCase))

            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = error });

        if (error != null)

            return BadRequest(new { message = error });



        _logger.LogInformation(

            "Quick tenant user created for {TenantId}: {Email} role={Role} by {Actor}",

            tenantId,

            result!.Email,

            request.Role,

            ActorUserId);



        return CreatedAtAction(nameof(List), new { tenantId }, result);

    }



    /// <summary>Assign an existing platform user to the tenant.</summary>

    [HttpPost("assign")]

    [ProducesResponseType(typeof(TenantUserDto), StatusCodes.Status201Created)]

    [ProducesResponseType(StatusCodes.Status400BadRequest)]

    [ProducesResponseType(StatusCodes.Status404NotFound)]

    public async Task<ActionResult<TenantUserDto>> AssignExisting(

        Guid tenantId,

        [FromBody] AddAdminTenantUserRequest request,

        CancellationToken cancellationToken = default)

    {

        if (!ModelState.IsValid)

            return ValidationProblem(ModelState);



        var (result, error) = await _tenantUserService

            .AssignExistingAsync(tenantId, request, cancellationToken)

            .ConfigureAwait(false);

        if (error == "Tenant not found.")

            return NotFound(new { message = error });

        if (error != null)

            return BadRequest(new { message = error });



        return CreatedAtAction(nameof(List), new { tenantId }, result);

    }



    /// <summary>Legacy invite route — creates user with password or assigns existing (no email).</summary>

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



        var (result, error) = await _tenantUserService

            .InviteAsync(tenantId, request, ActorUserId, cancellationToken)

            .ConfigureAwait(false);

        if (error == "Tenant not found.")

            return NotFound(new { message = error });

        if (error != null)

            return BadRequest(new { message = error });



        _logger.LogInformation(

            "Tenant user invite processed for {TenantId}: {Email}, created={Created}",

            tenantId,

            request.Email,

            result!.UserCreated);



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


