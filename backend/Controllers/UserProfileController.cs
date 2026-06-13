using KasseAPI_Final.Auth;
using KasseAPI_Final.Data;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/user/profile")]
[Produces("application/json")]
public class UserProfileController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;
    private readonly IUserUniquenessValidationService _uniquenessValidation;
    private readonly IAuditLogService _auditLogService;
    private readonly IUserSessionInvalidation _sessionInvalidation;
    private readonly IUserUsernameHistoryService _usernameHistory;
    private readonly IUsernameChangeEmailService _usernameChangeEmail;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ILogger<UserProfileController> _logger;

    public UserProfileController(
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        IUserUniquenessValidationService uniquenessValidation,
        IAuditLogService auditLogService,
        IUserSessionInvalidation sessionInvalidation,
        IUserUsernameHistoryService usernameHistory,
        IUsernameChangeEmailService usernameChangeEmail,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<UserProfileController> logger)
    {
        _userManager = userManager;
        _context = context;
        _uniquenessValidation = uniquenessValidation;
        _auditLogService = auditLogService;
        _sessionInvalidation = sessionInvalidation;
        _usernameHistory = usernameHistory;
        _usernameChangeEmail = usernameChangeEmail;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<UserProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        var user = await FindActiveCurrentUserAsync(cancellationToken).ConfigureAwait(false);
        if (user == null)
            return NotFound(new { message = "User not found" });

        return Ok(MapToDto(user));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { message = "Request body is required.", code = "VALIDATION_ERROR" });

        if (!ModelState.IsValid)
            return BadRequest(new { message = "Validation failed.", code = "VALIDATION_ERROR", errors = ModelState });

        var currentUserId = User.GetActorUserId();
        if (string.IsNullOrEmpty(currentUserId))
            return Unauthorized(new { message = "User not authenticated" });

        var user = await FindActiveCurrentUserAsync(cancellationToken).ConfigureAwait(false);
        if (user == null)
            return NotFound(new { message = "User not found" });

        try
        {
            var oldSnapshot = UserAuditDiffHelper.CreateSafeSnapshot(user);
            var normalizedEmail = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

            var (hasConflict, conflictMessage) = await _uniquenessValidation.ValidateUniquenessForUpdateAsync(
                currentUserId: user.Id,
                user.Email,
                user.EmployeeNumber,
                user.TaxNumber,
                normalizedEmail,
                user.EmployeeNumber,
                user.TaxNumber).ConfigureAwait(false);

            if (hasConflict)
                return BadRequest(new { message = conflictMessage, code = "VALIDATION_ERROR" });

            user.FirstName = request.FirstName.Trim();
            user.LastName = request.LastName.Trim();

            if (!string.IsNullOrEmpty(normalizedEmail)
                && !string.Equals(normalizedEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = normalizedEmail;
                user.NormalizedEmail = _userManager.NormalizeEmail(normalizedEmail);
            }
            else if (string.IsNullOrEmpty(normalizedEmail))
            {
                user.Email = null;
                user.NormalizedEmail = null;
            }

            user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
            user.UpdatedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user).ConfigureAwait(false);
            if (!result.Succeeded)
                return BadRequest(new { message = "Failed to update profile", errors = result.Errors });

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var actorRole = User.GetActorRole() ?? "Unknown";
            var newSnapshot = UserAuditDiffHelper.CreateSafeSnapshot(user);
            await TryLogProfileUpdatedAsync(currentUserId, actorRole, oldSnapshot, newSnapshot, user.UserName)
                .ConfigureAwait(false);

            return Ok(new { message = "Profile updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for user {UserId}", currentUserId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>Self-service login username change. Audited; invalidates active sessions.</summary>
    [HttpPatch("username")]
    public async Task<IActionResult> UpdateOwnUsername(
        [FromBody] UpdateUsernameRequest? request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { message = "Request body is required.", code = "VALIDATION_ERROR" });

        var currentUserId = User.GetActorUserId();
        if (string.IsNullOrEmpty(currentUserId))
            return Unauthorized(new { message = "User not authenticated" });

        var user = await FindActiveCurrentUserAsync(cancellationToken).ConfigureAwait(false);
        if (user == null)
            return NotFound(new { message = "User not found" });

        var newUsername = request.NewUsername.Trim();
        var actorRole = User.GetActorRole() ?? "Unknown";
        var bypassRestrictions = UsernameChangeRestrictions.IsBypassedForActor(actorRole);

        var validationErrors = UsernameValidation.ValidateNewUsername(
            newUsername,
            bypassReservedUsername: bypassRestrictions);
        if (validationErrors != null)
            return BadRequest(new { message = "Validation failed.", code = "VALIDATION_ERROR", errors = validationErrors });

        var oldUsername = user.UserName;
        if (string.Equals(oldUsername, newUsername, StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { oldUsername, newUsername = user.UserName ?? newUsername, message = "Username unchanged." });
        }

        var rateLimitError = await UsernameChangeRateLimit
            .GetRateLimitErrorAsync(_userManager, user, bypassCooldown: bypassRestrictions, cancellationToken)
            .ConfigureAwait(false);
        if (rateLimitError != null)
            return BadRequest(new { message = rateLimitError, code = "BUSINESS_RULE" });

        var newAccountError = UsernameChangePolicy.GetNewAccountRestrictionError(user, bypassRestrictions);
        if (newAccountError != null)
            return BadRequest(new { message = newAccountError, code = "BUSINESS_RULE" });

        if (await _uniquenessValidation.IsUserNameTakenByOtherUserAsync(newUsername, user.Id).ConfigureAwait(false))
            return Conflict(new { message = UsernameConflictMessages.Detail(newUsername), code = "USERNAME_CONFLICT" });

        var setNameResult = await _userManager.SetUserNameAsync(user, newUsername).ConfigureAwait(false);
        if (!setNameResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "Username update failed",
                code = "VALIDATION_ERROR",
                errors = setNameResult.Errors.Select(e => e.Description),
            });
        }

        user.UpdatedAt = DateTime.UtcNow;
        var updateResult = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!updateResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "Username update failed",
                code = "VALIDATION_ERROR",
                errors = updateResult.Errors.Select(e => e.Description),
            });
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var tenantId = _tenantAccessor.TenantId;

        await _auditLogService.LogUserLifecycleAsync(
            AuditEventType.UserNameChanged,
            currentUserId,
            actorRole,
            currentUserId,
            tenantId,
            reason,
            null,
            AuditLogStatus.Success,
            description: $"Username changed from '{oldUsername}' to '{newUsername}'",
            oldValues: new { UserName = oldUsername },
            newValues: new { UserName = newUsername }).ConfigureAwait(false);

        await InvalidateSessionsAfterUsernameChangeAsync(currentUserId, user, cancellationToken).ConfigureAwait(false);

        await _usernameHistory.RecordChangeAsync(
            currentUserId,
            oldUsername,
            newUsername,
            currentUserId,
            reason,
            cancellationToken).ConfigureAwait(false);

        await UsernameChangeRateLimit.RecordChangeAsync(_userManager, user, cancellationToken).ConfigureAwait(false);
        await TryNotifyUsernameChangedAsync(user, oldUsername, newUsername).ConfigureAwait(false);

        return Ok(new
        {
            oldUsername,
            newUsername = user.UserName ?? newUsername,
            message = "Username updated successfully. Please sign in again.",
        });
    }

    private async Task InvalidateSessionsAfterUsernameChangeAsync(
        string userId,
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var stampResult = await _userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        if (!stampResult.Succeeded)
        {
            _logger.LogWarning(
                "Security stamp update failed after self-service username change for user {UserId}",
                userId);
        }

        await _sessionInvalidation.InvalidateSessionsForUserAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    private async Task TryNotifyUsernameChangedAsync(
        ApplicationUser user,
        string? oldUsername,
        string newUsername)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return;

        var actorEmail = user.Email.Trim();
        await _usernameChangeEmail.TrySendUsernameChangedAsync(
            new UsernameChangedEmailRequest(
                user.Email.Trim(),
                oldUsername ?? string.Empty,
                newUsername,
                actorEmail,
                DateTime.UtcNow)).ConfigureAwait(false);
    }

    private async Task<ApplicationUser?> FindActiveCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return null;

        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user == null || !user.IsActive)
            return null;

        return user;
    }

    private static UserProfileDto MapToDto(ApplicationUser user) => new()
    {
        Id = user.Id,
        UserName = user.UserName ?? string.Empty,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Role = RoleCanonicalization.GetCanonicalRole(user.Role),
        EmployeeNumber = user.EmployeeNumber,
        PhoneNumber = user.PhoneNumber,
    };

    private async Task TryLogProfileUpdatedAsync(
        string actorUserId,
        string actorRole,
        object? oldSnapshot,
        object newSnapshot,
        string? userName)
    {
        try
        {
            await _auditLogService.LogUserLifecycleAsync(
                AuditLogActions.USER_UPDATE,
                actorUserId,
                actorRole,
                actorUserId,
                null,
                null,
                AuditLogStatus.Success,
                $"User updated own profile: {userName}",
                oldSnapshot,
                newSnapshot).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Profile update audit failed (primary operation succeeded). ActorUserId: {ActorUserId}",
                actorUserId);
        }
    }
}
