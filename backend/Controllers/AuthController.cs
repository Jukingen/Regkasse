using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KasseAPI_Final.Auth;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Localization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Auth;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Services.Localization;
using KasseAPI_Final.Services.Token;
using KasseAPI_Final.Services.TwoFactor;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KasseAPI_Final.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    /// <summary>
    /// Auth endpoints. Effective permissions for JSON responses (login user payload, GET /me) must match JWT
    /// <c>permission</c> claims: both use <see cref="IRolePermissionResolver"/> (system roles → RolePermissionMatrix;
    /// custom roles → AspNetRoleClaims), same as <see cref="ITokenClaimsService.BuildClaimsAsync"/>.
    /// Tenant fields on login come from <see cref="ILoginTenantResolver"/> (membership, else legacy default).
    /// GET /me uses <see cref="IAuthTenantSnapshotProvider"/> (JWT <c>tenant_id</c> when valid, else default).
    /// </summary>
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;
        private readonly ITokenClaimsService _tokenClaimsService;
        private readonly IEffectivePermissionResolver _effectivePermissionResolver;
        private readonly AuthOptions _authOptions;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly IAuthTenantSnapshotProvider _authTenantSnapshotProvider;
        private readonly ILoginTenantResolver _loginTenantResolver;
        private readonly IAuthService _authService;
        private readonly IUserTenantMembershipProvisioner _tenantMembershipProvisioner;
        private readonly IForgotUsernameEmailService _forgotUsernameEmail;
        private readonly IForgotPasswordEmailService _forgotPasswordEmail;
        private readonly ITenantSessionPolicyService _sessionPolicyService;
        private readonly ISessionService _sessionService;
        private readonly IApiMessageLocalizer _messages;
        private readonly II18nErrorService _i18nErrorService;
        private readonly IPosShiftService _posShiftService;
        private readonly IAccountLockoutService _accountLockoutService;
        private readonly IHostEnvironment _environment;
        private readonly ITwoFactorChallengeService _twoFactorChallengeService;
        private readonly ITwoFactorService _twoFactorService;
        private readonly TwoFactorAuthOptions _twoFactorAuthOptions;
        private readonly ITokenBlacklistService _tokenBlacklistService;
        private readonly IAuditLogService _auditLogService;

        /// <summary>Throttles diagnostic logs when /me is called without a resolvable user id claim.</summary>
        private static readonly object GetCurrentUserMissingIdLogSync = new();
        private static DateTime s_lastGetCurrentUserMissingIdLogUtc = DateTime.MinValue;

        public AuthController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ILogger<AuthController> logger,
            ITokenClaimsService tokenClaimsService,
            IEffectivePermissionResolver effectivePermissionResolver,
            IOptions<AuthOptions> authOptions,
            IRefreshTokenService refreshTokenService,
            IAuthTenantSnapshotProvider authTenantSnapshotProvider,
            ILoginTenantResolver loginTenantResolver,
            IAuthService authService,
            IUserTenantMembershipProvisioner tenantMembershipProvisioner,
            IForgotUsernameEmailService forgotUsernameEmail,
            IForgotPasswordEmailService forgotPasswordEmail,
            ITenantSessionPolicyService sessionPolicyService,
            ISessionService sessionService,
            IApiMessageLocalizer messages,
            II18nErrorService i18nErrorService,
            IPosShiftService posShiftService,
            IAccountLockoutService accountLockoutService,
            IHostEnvironment environment,
            ITwoFactorChallengeService twoFactorChallengeService,
            ITwoFactorService twoFactorService,
            IOptions<TwoFactorAuthOptions> twoFactorAuthOptions,
            ITokenBlacklistService tokenBlacklistService,
            IAuditLogService auditLogService)
        {
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
            _logger = logger;
            _tokenClaimsService = tokenClaimsService;
            _effectivePermissionResolver = effectivePermissionResolver;
            _authOptions = authOptions.Value;
            _refreshTokenService = refreshTokenService;
            _authTenantSnapshotProvider = authTenantSnapshotProvider;
            _loginTenantResolver = loginTenantResolver;
            _authService = authService;
            _tenantMembershipProvisioner = tenantMembershipProvisioner;
            _forgotUsernameEmail = forgotUsernameEmail;
            _forgotPasswordEmail = forgotPasswordEmail;
            _sessionPolicyService = sessionPolicyService;
            _sessionService = sessionService;
            _messages = messages;
            _i18nErrorService = i18nErrorService;
            _posShiftService = posShiftService;
            _accountLockoutService = accountLockoutService;
            _environment = environment;
            _twoFactorChallengeService = twoFactorChallengeService;
            _twoFactorService = twoFactorService;
            _twoFactorAuthOptions = twoFactorAuthOptions.Value;
            _tokenBlacklistService = tokenBlacklistService;
            _auditLogService = auditLogService;
        }

        /// <summary>
        /// Extends the current auth session idle window by updating last-activity on <c>auth_sessions</c>.
        /// </summary>
        [Authorize]
        [HttpPost("refresh-session")]
        public async Task<IActionResult> RefreshSession(CancellationToken cancellationToken = default)
        {
            var userId = User.GetActorUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "User not authenticated" });

            var sidRaw = User.FindFirst("sid")?.Value;
            if (!Guid.TryParse(sidRaw, out var sessionId))
                return NoContent();

            await _sessionService.TouchSessionActivityAsync(sessionId, cancellationToken).ConfigureAwait(false);
            return Ok(new { message = "Session refreshed" });
        }

        /// <summary>
        /// Sends the current login username for the given email (admin app). Always returns success to avoid account enumeration.
        /// Username change history remains in audit tables only — not included in this email.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("forgot-username")]
        public async Task<IActionResult> ForgotUsername(
            [FromBody] ForgotUsernameRequest? request,
            CancellationToken cancellationToken = default)
        {
            const string genericMessage =
                "If an account exists for this email, we sent your login usernames. Check your inbox.";

            if (request == null || string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Email is required." });

            var clientApp = request.ClientApp?.Trim().ToLowerInvariant();
            if (!string.Equals(clientApp, ClientAppPolicy.Admin, StringComparison.Ordinal))
                return BadRequest(new { message = "clientApp must be \"admin\" for this endpoint." });

            var email = request.Email.Trim();
            var user = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
            if (user == null || !user.IsActive)
            {
                _logger.LogInformation("Forgot-username: no active user for masked email.");
                return Ok(new { message = genericMessage });
            }

            var usernames = string.IsNullOrWhiteSpace(user.UserName)
                ? Array.Empty<string>()
                : new[] { user.UserName.Trim() };

            if (usernames.Length > 0)
            {
                string? devSummary = null;
                if (HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true)
                {
                    devSummary =
                        $"Matched account: role={user.Role}, current username={user.UserName}, userId={user.Id}";
                }

                var sent = await _forgotUsernameEmail.TrySendForgotUsernameAsync(
                    new ForgotUsernameEmailRequest(email, usernames, devSummary),
                    cancellationToken).ConfigureAwait(false);

                if (!sent)
                {
                    _logger.LogWarning(
                        "Forgot-username: SMTP not configured or send failed for user {UserId}.",
                        user.Id);
                }
            }

            return Ok(new { message = genericMessage });
        }

        /// <summary>
        /// Sends a password reset email for the given address (admin app). Always returns success to avoid account enumeration.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(
            [FromBody] ForgotPasswordRequest? request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Email is required." });

            var clientApp = request.ClientApp?.Trim().ToLowerInvariant();
            if (!string.Equals(clientApp, ClientAppPolicy.Admin, StringComparison.Ordinal))
                return BadRequest(new { message = "clientApp must be \"admin\" for this endpoint." });

            var email = request.Email.Trim();
            var user = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
            if (user != null && user.IsActive)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
                var sent = await _forgotPasswordEmail.TrySendForgotPasswordAsync(
                    new ForgotPasswordEmailRequest(email, token),
                    cancellationToken).ConfigureAwait(false);

                if (!sent)
                {
                    _logger.LogWarning(
                        "Forgot-password: SMTP not configured or send failed for user {UserId}.",
                        user.Id);
                }
            }
            else
            {
                _logger.LogInformation("Forgot-password: no active user for masked email.");
            }

            return Ok(new { message = _messages.Get(ApiMessageKeys.ForgotPasswordGeneric) });
        }

        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login(LoginModel model)
        {
            string? loginIdentifier = null;
            try
            {
#pragma warning disable CS0618 // Legacy email login fallback
                var identifier = model.LoginIdentifier ?? model.Email;
#pragma warning restore CS0618
                if (string.IsNullOrWhiteSpace(identifier))
                    return BadRequest(new { message = "Login identifier required" });

                loginIdentifier = identifier.Trim();
                _logger.LogInformation(
                    "Login attempt for user: {LoginMasked}, clientApp: {ClientApp}",
                    MaskLoginIdentifier(loginIdentifier),
                    model.ClientApp ?? "(none)");

                if (_accountLockoutService.IsLockedOut(loginIdentifier))
                {
                    _logger.LogWarning(
                        "Login blocked: account temporarily locked for identifier {LoginMasked}",
                        MaskLoginIdentifier(loginIdentifier));
                    return Unauthorized(new ApiErrorResponse
                    {
                        Code = "ACCOUNT_LOCKED",
                        Message = _messages.Get(ApiMessageKeys.AccountTemporarilyLocked),
                    });
                }

                if (string.IsNullOrWhiteSpace(model.Password))
                {
                    return BadRequest(new { message = "Password required" });
                }

                // --- clientApp validation (fail-closed) ---
                var allowLegacy = _authOptions.AllowLegacyLoginWithoutClientApp;

                string? resolvedClientApp = model.ClientApp?.Trim().ToLowerInvariant();

                if (string.IsNullOrEmpty(resolvedClientApp))
                {
                    if (!allowLegacy)
                    {
                        return BadRequest(new { message = "clientApp field is required (\"pos\" or \"admin\")." });
                    }

                    _logger.LogWarning(
                        "Login without clientApp from {LoginMasked}. AllowLegacyLoginWithoutClientApp is ON — legacy mode.",
                        MaskLoginIdentifier(loginIdentifier));
                    resolvedClientApp = null;
                }
                else if (!ClientAppPolicy.IsKnownApp(resolvedClientApp))
                {
                    return BadRequest(new { message = $"Unknown clientApp value: \"{model.ClientApp}\". Allowed: pos, admin." });
                }

                var loginCancellation = HttpContext?.RequestAborted ?? CancellationToken.None;
                var user = await IdentityLoginLookup.FindByLoginIdentifierAsync(
                    _userManager,
                    loginIdentifier,
                    loginCancellation);
                if (user == null)
                {
                    _accountLockoutService.RecordFailedAttempt(loginIdentifier);
                    return await InvalidLoginCredentialsAsync(
                        "User not found",
                        loginMasked: MaskLoginIdentifier(loginIdentifier));
                }

                if (!user.IsActive)
                {
                    return await InvalidLoginCredentialsAsync("Inactive user", userId: user.Id);
                }

                var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
                if (!passwordValid)
                {
                    _accountLockoutService.RecordFailedAttempt(loginIdentifier);
                    return await InvalidLoginCredentialsAsync("Invalid password", userId: user.Id);
                }

                _accountLockoutService.ResetAttempts(loginIdentifier);

                // --- Role-level app policy check ---
                var roles = await _userManager.GetRolesAsync(user);
                var canonicalRoles = TokenClaimsService.CollectCanonicalRoles(roles, user.Role);
                var primaryRole = TokenClaimsService.ResolvePrimaryRole(canonicalRoles);
                var canonicalRole = RoleCanonicalization.GetCanonicalRole(primaryRole);

                if (resolvedClientApp != null && !ClientAppPolicy.IsRoleAllowedForApp(resolvedClientApp, roles.Append(primaryRole)))
                {
                    _logger.LogWarning(
                        "Login denied: user {EmailMasked} (role {Role}) is not allowed for clientApp {ClientApp}",
                        MaskLoginIdentifier(loginIdentifier), canonicalRole, resolvedClientApp);

                    return StatusCode(403, new { message = _messages.Get(ApiMessageKeys.NotAuthorizedForApp) });
                }

                var authCt = HttpContext?.RequestAborted ?? CancellationToken.None;
                if (_authOptions.RequireTenantMembershipForLogin)
                {
                    var hasMembership = await _loginTenantResolver.HasActiveMembershipAsync(user.Id, authCt);
                    if (!hasMembership)
                    {
                        _logger.LogWarning(
                            "Login denied: RequireTenantMembershipForLogin is enabled but user {UserId} has no active tenant membership.",
                            user.Id);
                        return BadRequest(new
                        {
                            message = _messages.Get(ApiMessageKeys.TenantMembershipRequired),
                            code = "TENANT_MEMBERSHIP_REQUIRED",
                        });
                    }
                }

                var tenantAccess = await _authService.ResolveLoginTenantAccessAsync(
                    user.Id,
                    string.Equals(canonicalRole, Roles.SuperAdmin, StringComparison.Ordinal),
                    authCt);
                if (!tenantAccess.Allowed)
                {
                    return BadRequest(new
                    {
                        message = tenantAccess.Message,
                        code = tenantAccess.Code,
                    });
                }

                if (tenantAccess.Snapshot is not AuthTenantSnapshot loginTenantSnapshot)
                {
                    return BadRequest(new
                    {
                        message = _messages.Get(ApiMessageKeys.TenantMembershipRequired),
                        code = "TENANT_MEMBERSHIP_REQUIRED",
                    });
                }

                // Production SuperAdmin: require TOTP before issuing tokens. Development bypasses 2FA.
                if (IsSuperAdminTwoFactorRequired(canonicalRole))
                {
                    return await BuildTwoFactorChallengeResponseAsync(
                        user,
                        loginIdentifier,
                        resolvedClientApp,
                        authCt).ConfigureAwait(false);
                }

                return await CompleteSuccessfulLoginAsync(
                    user,
                    roles,
                    canonicalRole,
                    resolvedClientApp,
                    loginIdentifier,
                    loginTenantSnapshot,
                    authCt).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for user: {LoginMasked}", MaskLoginIdentifier(loginIdentifier));
                return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.LoginError) });
            }
        }

        /// <summary>
        /// Completes SuperAdmin login after TOTP verification (Production only; Development skips 2FA at login).
        /// </summary>
        [AllowAnonymous]
        [HttpPost("verify-2fa")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorModel? model)
        {
            if (model is null
                || string.IsNullOrWhiteSpace(model.TwoFactorToken)
                || string.IsNullOrWhiteSpace(model.Code))
            {
                return BadRequest(new { message = "twoFactorToken and code are required." });
            }

            if (!_twoFactorChallengeService.TryConsumeChallenge(model.TwoFactorToken.Trim(), out var challenge)
                || challenge is null)
            {
                return Unauthorized(new ApiErrorResponse
                {
                    Code = "TWO_FACTOR_CHALLENGE_EXPIRED",
                    Message = _messages.Get(ApiMessageKeys.TwoFactorChallengeExpired),
                });
            }

            var authCt = HttpContext?.RequestAborted ?? CancellationToken.None;
            var user = await _userManager.FindByIdAsync(challenge.UserId).ConfigureAwait(false);
            if (user is null || !user.IsActive)
            {
                return Unauthorized(new ApiErrorResponse
                {
                    Code = "INVALID_CREDENTIALS",
                    Message = _messages.Get(ApiMessageKeys.InvalidLoginCredentials),
                });
            }

            var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
            var canonicalRoles = TokenClaimsService.CollectCanonicalRoles(roles, user.Role);
            var primaryRole = TokenClaimsService.ResolvePrimaryRole(canonicalRoles);
            var canonicalRole = RoleCanonicalization.GetCanonicalRole(primaryRole);

            if (!string.Equals(canonicalRole, Roles.SuperAdmin, StringComparison.Ordinal))
            {
                return Unauthorized(new ApiErrorResponse
                {
                    Code = "TWO_FACTOR_INVALID",
                    Message = _messages.Get(ApiMessageKeys.TwoFactorInvalid),
                });
            }

            var codeValid = await _twoFactorService
                .VerifyTwoFactorTokenAsync(user, model.Code, authCt)
                .ConfigureAwait(false);

            if (!codeValid)
            {
                _accountLockoutService.RecordFailedAttempt(challenge.LoginIdentifier);
                _logger.LogWarning("2FA verification failed for user {UserId}", user.Id);
                return Unauthorized(new ApiErrorResponse
                {
                    Code = "TWO_FACTOR_INVALID",
                    Message = _messages.Get(ApiMessageKeys.TwoFactorInvalid),
                });
            }

            _accountLockoutService.ResetAttempts(challenge.LoginIdentifier);

            if (challenge.SetupRequired || !await _userManager.GetTwoFactorEnabledAsync(user).ConfigureAwait(false))
            {
                var enableResult = await _userManager.SetTwoFactorEnabledAsync(user, true).ConfigureAwait(false);
                if (!enableResult.Succeeded)
                {
                    _logger.LogWarning(
                        "Failed to enable 2FA for user {UserId}: {Errors}",
                        user.Id,
                        string.Join("; ", enableResult.Errors.Select(e => e.Description)));
                    return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.LoginError) });
                }
            }

            var tenantAccess = await _authService.ResolveLoginTenantAccessAsync(
                user.Id,
                isSuperAdmin: true,
                authCt).ConfigureAwait(false);
            if (!tenantAccess.Allowed || tenantAccess.Snapshot is not AuthTenantSnapshot loginTenantSnapshot)
            {
                return BadRequest(new
                {
                    message = tenantAccess.Message ?? _messages.Get(ApiMessageKeys.TenantMembershipRequired),
                    code = tenantAccess.Code ?? "TENANT_MEMBERSHIP_REQUIRED",
                });
            }

            return await CompleteSuccessfulLoginAsync(
                user,
                roles,
                canonicalRole,
                challenge.ClientApp,
                challenge.LoginIdentifier,
                loginTenantSnapshot,
                authCt).ConfigureAwait(false);
        }

        private async Task<IActionResult> BuildTwoFactorChallengeResponseAsync(
            ApplicationUser user,
            string loginIdentifier,
            string? resolvedClientApp,
            CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            var setupRequired = !await _userManager.GetTwoFactorEnabledAsync(user).ConfigureAwait(false);
            string? authenticatorKey = null;
            string? authenticatorUri = null;

            if (setupRequired)
            {
                await _userManager.ResetAuthenticatorKeyAsync(user).ConfigureAwait(false);
                authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(user).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(authenticatorKey))
                {
                    _logger.LogError("Failed to create authenticator key for SuperAdmin {UserId}", user.Id);
                    return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.LoginError) });
                }

                authenticatorUri = BuildAuthenticatorUri(user, authenticatorKey);
            }

            var expiresAtUtc = DateTime.UtcNow.AddMinutes(5);
            var token = _twoFactorChallengeService.CreateChallenge(new TwoFactorChallengePayload(
                user.Id,
                resolvedClientApp,
                loginIdentifier,
                setupRequired,
                expiresAtUtc));

            _logger.LogInformation(
                "2FA challenge issued for SuperAdmin {UserId} (setupRequired={SetupRequired})",
                user.Id,
                setupRequired);

            return Ok(new LoginTwoFactorChallengeDto
            {
                Requires2FA = true,
                Requires2FASetup = setupRequired,
                TwoFactorToken = token,
                IsDevelopment = _twoFactorService.IsDevelopment,
                AuthenticatorKey = authenticatorKey,
                AuthenticatorUri = authenticatorUri,
                DevelopmentBypassCode = _twoFactorService.GenerateTwoFactorToken(user),
            });
        }

        private async Task<IActionResult> CompleteSuccessfulLoginAsync(
            ApplicationUser user,
            IList<string> roles,
            string canonicalRole,
            string? resolvedClientApp,
            string loginIdentifier,
            AuthTenantSnapshot loginTenantSnapshot,
            CancellationToken authCt)
        {
            // Persist last login for audit and UI (Users list / detail).
            user.LastLoginAt = DateTime.UtcNow;
            user.LoginCount++;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogWarning("LastLoginAt/LoginCount update failed for user {UserId}, login will continue. Errors: {Errors}",
                    user.Id, string.Join("; ", updateResult.Errors.Select(e => e.Description)));
            }

            Guid? sessionTenantKey = Guid.TryParse(loginTenantSnapshot.TenantId, out var loginTenantGuid)
                ? loginTenantGuid
                : null;

            var issuedTokens = await _refreshTokenService.IssueLoginTokensAsync(
                user.Id,
                resolvedClientApp ?? "legacy",
                async (tokenUserId, jti, sessionId, expiresAtUtc, clientApp, persistedSessionTenantId) =>
                {
                    var issuance = await _authTenantSnapshotProvider.ResolveForTokenIssuanceAsync(
                        persistedSessionTenantId,
                        user: null,
                        authCt);
                    var claims = await _tokenClaimsService.BuildClaimsAsync(
                        user,
                        roles,
                        tenantId: issuance.TenantId,
                        branchId: issuance.BranchId,
                        appContext: resolvedClientApp);
                    return GenerateJwtToken(claims, jti, sessionId, expiresAtUtc);
                },
                sessionTenantId: sessionTenantKey,
                clientMetadata: BuildSessionClientMetadata(),
                authCt);
            var permissions = await GetEffectivePermissionsListAsync(
                user.Id,
                roles,
                user.Role,
                sessionTenantKey,
                resolvedClientApp,
                authCt);

            var response = new
            {
                token = issuedTokens.AccessToken,
                expiresIn = Math.Max(60, (int)(issuedTokens.AccessTokenExpiresAtUtc - DateTime.UtcNow).TotalSeconds),
                expiresAt = issuedTokens.AccessTokenExpiresAtUtc,
                refreshToken = issuedTokens.RefreshToken,
                refreshTokenExpiresAtUtc = issuedTokens.RefreshTokenExpiresAtUtc,
                requires2FA = false,
                isDevelopment = _environment.IsDevelopment(),
                user = new
                {
                    id = user.Id,
                    userName = user.UserName,
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    role = canonicalRole,
                    roles = roles,
                    permissions = permissions,
                    isDemo = user.IsDemo,
                    tenantId = loginTenantSnapshot.TenantId,
                    tenantDisplayName = loginTenantSnapshot.TenantDisplayName,
                    tenantSlug = loginTenantSnapshot.TenantSlug,
                    branchId = loginTenantSnapshot.BranchId,
                    branchDisplayName = loginTenantSnapshot.BranchDisplayName,
                    mustChangePasswordOnNextLogin = user.MustChangePasswordOnNextLogin,
                },
                appContext = resolvedClientApp
            };

            _logger.LogInformation(
                "Login successful for user: {LoginMasked}, clientApp: {ClientApp}",
                MaskLoginIdentifier(loginIdentifier),
                resolvedClientApp ?? "legacy");

            await TryLogLoginAuditAsync(
                AuditEventType.LoginSuccess,
                user.Id,
                canonicalRole,
                sessionTenantKey,
                description: $"Login success (clientApp={resolvedClientApp ?? "legacy"})",
                status: AuditLogStatus.Success).ConfigureAwait(false);

            return Ok(response);
        }

        private bool IsSuperAdminTwoFactorRequired(string canonicalRole)
        {
            if (!string.Equals(canonicalRole, Roles.SuperAdmin, StringComparison.Ordinal))
                return false;

            if (!_twoFactorAuthOptions.Enabled)
                return false;

            // Fail-closed: BypassInDevelopment only applies in Development.
            if (_environment.IsDevelopment() && _twoFactorAuthOptions.BypassInDevelopment)
                return false;

            // Legacy Auth:RequireSuperAdminTwoFactor override (unit tests / forced staging).
            if (_authOptions.RequireSuperAdminTwoFactor is bool forced)
                return forced;

            // Default: require outside Development (Production / Staging).
            return !_environment.IsDevelopment();
        }

        private string BuildAuthenticatorUri(ApplicationUser user, string unformattedKey)
        {
            var issuer = Uri.EscapeDataString("Regkasse");
            var account = Uri.EscapeDataString(
                user.Email ?? user.UserName ?? user.Id);
            return $"otpauth://totp/{issuer}:{account}?secret={unformattedKey}&issuer={issuer}&digits=6";
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userId = User.GetActorUserId()
                    ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("Logout requested for user: {UserId}", userId);

                // Immediate access-token revocation (complements session revoke below).
                BlacklistCurrentAccessToken();

                var sidRaw = User.FindFirst("sid")?.Value;
                if (Guid.TryParse(sidRaw, out var sessionId))
                {
                    await _refreshTokenService.LogoutSessionAsync(sessionId, "logout");
                }

                // 🧹 KULLANICI SEPETLERİNİ TEMİZLE
                try
                {
                    // CartLifecycleService'i IServiceProvider üzerinden al
                    var cartLifecycleService = HttpContext.RequestServices.GetRequiredService<CartLifecycleService>();
                    await cartLifecycleService.CleanupUserCarts(userId);
                    _logger.LogInformation("User carts cleanup completed for user: {UserId}", userId);
                }
                catch (Exception cartCleanupEx)
                {
                    // Cart cleanup hatası logout'u engellemesin
                    _logger.LogWarning(cartCleanupEx, "Cart cleanup failed for user: {UserId}, but logout will continue", userId);
                }

                try
                {
                    var actorRole = User.GetActorRole() ?? Roles.FallbackUnknown;
                    await _posShiftService.AutoCloseShiftAsync(userId, actorRole);
                }
                catch (Exception shiftCloseEx)
                {
                    _logger.LogWarning(
                        shiftCloseEx,
                        "Auto-close cashier shift failed for user: {UserId}, but logout will continue",
                        userId);
                }

                // Audit: UserLogout (never log the raw JWT — blacklist stores a digest only).
                await TryLogLogoutAuditAsync(userId, reason: "logout");

                _logger.LogInformation("Logout successful for user: {UserId}", userId);
                return Ok(new { success = true, message = "Logout successful" });
            }
            catch (Exception ex)
            {
                var userId = User.GetActorUserId()
                    ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? "Unknown";
                _logger.LogError(ex, "Logout error for user: {UserId}. Exception: {ExceptionType}, Message: {ExceptionMessage}",
                    userId, ex.GetType().Name, ex.Message);

                return StatusCode(500, new { message = "Logout failed due to a server error." });
            }
        }

        /// <summary>GET /me — returns current user; 401 if token missing/invalid or no user id claims.</summary>
        [Authorize]
        [HttpGet("me")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                if (User?.Identity?.IsAuthenticated != true)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var userId = User.GetActorUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    var now = DateTime.UtcNow;
                    lock (GetCurrentUserMissingIdLogSync)
                    {
                        if (now - s_lastGetCurrentUserMissingIdLogUtc >= TimeSpan.FromMinutes(1))
                        {
                            s_lastGetCurrentUserMissingIdLogUtc = now;
                            _logger.LogDebug(
                                "GetCurrentUser: User ID not found in token claims (no resolvable user id); returning 401. Suppressing similar logs for 60s.");
                        }
                    }

                    return Unauthorized(new { message = "User not authenticated" });
                }

                _logger.LogInformation("GetCurrentUser: User ID from token: {UserId}", userId);

                // Kullanıcıyı veritabanından bul
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("GetCurrentUser: User not found in database for ID: {UserId}", userId);
                    return NotFound(new { message = "User not found" });
                }

                if (!user.IsActive)
                {
                    _logger.LogWarning("GetCurrentUser: User account is not active for ID: {UserId}", userId);
                    return BadRequest(new { message = "User account is not active" });
                }

                // Same effective permission source as JWT (IEffectivePermissionResolver / TokenClaimsService).
                var roles = await _userManager.GetRolesAsync(user);
                var canonicalRoles = TokenClaimsService.CollectCanonicalRoles(roles, user.Role);
                var primaryRole = TokenClaimsService.ResolvePrimaryRole(canonicalRoles);
                var meCt = HttpContext?.RequestAborted ?? CancellationToken.None;

                var tenantSnapshot = await _authTenantSnapshotProvider.GetSnapshotAsync(User, meCt);
                Guid? tenantGuid = Guid.TryParse(tenantSnapshot.TenantId, out var tid) ? tid : null;
                var appContext = User.FindFirst(ClientAppPolicy.AppContextClaimType)?.Value;
                var permissions = await GetEffectivePermissionsListAsync(
                    user.Id,
                    roles,
                    user.Role,
                    tenantGuid,
                    appContext,
                    meCt);
                var canonicalRole = RoleCanonicalization.GetCanonicalRole(primaryRole);

                var sessionPolicy = await _sessionPolicyService.GetPolicyAsync(tenantGuid, meCt);

                var userResponse = new
                {
                    id = user.Id,
                    userName = user.UserName,
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    employeeNumber = user.EmployeeNumber,
                    phoneNumber = user.PhoneNumber,
                    role = canonicalRole,
                    roles = roles,
                    permissions = permissions,
                    isDemo = user.IsDemo,
                    appContext = appContext,
                    tenantId = tenantSnapshot.TenantId,
                    tenantDisplayName = tenantSnapshot.TenantDisplayName,
                    tenantSlug = tenantSnapshot.TenantSlug,
                    branchId = tenantSnapshot.BranchId,
                    branchDisplayName = tenantSnapshot.BranchDisplayName,
                    mustChangePasswordOnNextLogin = user.MustChangePasswordOnNextLogin,
                    sessionPolicy,
                };

                _logger.LogInformation("GetCurrentUser: Successfully retrieved user {Email} with role {Role}, appContext {AppContext}", user.Email, user.Role, appContext ?? "none");
                return Ok(userResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetCurrentUser error. Exception: {ExceptionType}, Message: {ExceptionMessage}",
                    ex.GetType().Name, ex.Message);

                return StatusCode(500, new { message = "Error retrieving user information" });
            }
        }

        /// <summary>
        /// Rotates refresh + access tokens (opaque refresh rotation, reuse detection).
        /// Used by FA silent proactive refresh (~5 min before JWT <c>exp</c>) and 401 recovery.
        /// Optional <see cref="RefreshRequest.TenantId"/> rebinds JWT <c>tenant_id</c>.
        /// </summary>
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshRequest model)
        {
            try
            {
                _logger.LogInformation("Refresh token request received");

                if (string.IsNullOrWhiteSpace(model.RefreshToken))
                {
                    return BadRequest(new { message = "Refresh token is required" });
                }

                var refreshCt = HttpContext?.RequestAborted ?? CancellationToken.None;
                var tenantOverride = model.TenantId is Guid tid && tid != Guid.Empty ? tid : (Guid?)null;

                var result = await _refreshTokenService.RotateAsync(
                    model.RefreshToken,
                    async (tokenUserId, jti, sessionId, expiresAtUtc, clientApp, persistedSessionTenantId) =>
                    {
                        var user = await _userManager.FindByIdAsync(tokenUserId);
                        if (user == null)
                            throw new InvalidOperationException("Refresh token user not found");
                        var roles = await _userManager.GetRolesAsync(user);
                        var issuance = await _authTenantSnapshotProvider.ResolveForTokenIssuanceAsync(
                            persistedSessionTenantId,
                            user: null,
                            refreshCt);
                        var claims = await _tokenClaimsService.BuildClaimsAsync(
                            user,
                            roles,
                            tenantId: issuance.TenantId,
                            branchId: issuance.BranchId,
                            appContext: clientApp);
                        return GenerateJwtToken(claims, jti, sessionId, expiresAtUtc);
                    },
                    sessionTenantIdOverride: tenantOverride,
                    canAssignTenant: tenantOverride.HasValue
                        ? (userId, targetTenantId, ct) => CanAssignSessionTenantAsync(userId, targetTenantId, ct)
                        : null,
                    cancellationToken: refreshCt);

                if (!result.Success || result.Tokens == null)
                {
                    if (string.Equals(result.ErrorCode, "tenant_switch_forbidden", StringComparison.Ordinal))
                    {
                        return Unauthorized(new { message = "Refresh failed", code = result.ErrorCode });
                    }

                    var code = result.ReuseDetected ? "refresh_token_reuse_detected" : result.ErrorCode;
                    return Unauthorized(new { message = "Refresh failed", code });
                }

                var accessExpiresAt = result.Tokens.AccessTokenExpiresAtUtc;
                return Ok(new
                {
                    token = result.Tokens.AccessToken,
                    expiresIn = Math.Max(60, (int)(accessExpiresAt - DateTime.UtcNow).TotalSeconds),
                    expiresAt = accessExpiresAt,
                    refreshToken = result.Tokens.RefreshToken,
                    refreshTokenExpiresAtUtc = result.Tokens.RefreshTokenExpiresAtUtc
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refresh token error. Exception: {ExceptionType}, Message: {ExceptionMessage}",
                    ex.GetType().Name, ex.Message);

                return StatusCode(500, new { message = "Error during token refresh" });
            }
        }

        /// <summary>
        /// SuperAdmin may bind any active non-deleted tenant; other users need an active membership.
        /// </summary>
        private async Task<bool> CanAssignSessionTenantAsync(
            string userId,
            Guid targetTenantId,
            CancellationToken cancellationToken)
        {
            var tenantExists = await _context.Tenants.AsNoTracking()
                .AnyAsync(
                    t => t.Id == targetTenantId
                        && t.IsActive
                        && t.Status != TenantStatuses.Deleted,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!tenantExists)
            {
                return false;
            }

            var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
            if (user == null)
            {
                return false;
            }

            var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
            if (roles.Contains(Roles.SuperAdmin)
                || string.Equals(user.Role, Roles.SuperAdmin, StringComparison.Ordinal))
            {
                return true;
            }

            return await _context.UserTenantMemberships.IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    m => m.UserId == userId
                        && m.TenantId == targetTenantId
                        && m.IsActive,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterModel model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Email))
                    return BadRequest(new { message = "Email is required." });

                var email = model.Email.Trim();
                var regCt = HttpContext?.RequestAborted ?? default;
                var existingUser = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
                if (existingUser != null)
                {
                    _logger.LogWarning("Registration failed: email already registered (not disclosed to client).");
                    return RegistrationFailedResponse();
                }

                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    EmployeeNumber = model.EmployeeNumber ?? string.Empty,
                    IsActive = true
                };

                await using var tx = await _context.Database.BeginTransactionAsync(regCt);
                try
                {
                    var result = await _userManager.CreateAsync(user, model.Password);
                    if (!result.Succeeded)
                    {
                        await tx.RollbackAsync(regCt);
                        if (result.Errors.Any(IsDuplicateIdentityError))
                        {
                            _logger.LogWarning("Registration failed: duplicate identity (not disclosed to client).");
                            return RegistrationFailedResponse();
                        }

                        return BadRequest(new { errors = result.Errors });
                    }

                    var roleResult = await _userManager.AddToRoleAsync(user, Roles.Cashier);
                    if (!roleResult.Succeeded)
                    {
                        await tx.RollbackAsync(regCt);
                        return BadRequest(new { errors = roleResult.Errors });
                    }

                    await _tenantMembershipProvisioner.ProvisionActiveMembershipAsync(
                        user.Id,
                        LegacyDefaultTenantIds.Primary,
                        cancellationToken: regCt);
                    await tx.CommitAsync(regCt);
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync(regCt);
                    _logger.LogError(ex, "Registration failed (rolled back) for email {Email}", model.Email);
                    return StatusCode(500, new { message = "Registrierung fehlgeschlagen.", code = "REGISTRATION_TRANSACTION_FAILED" });
                }

                return Ok(new { message = _messages.Get(ApiMessageKeys.UserCreatedSuccess) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration error");
                return StatusCode(500, "Internal server error");
            }
        }

        [Authorize]
        [HttpPost("logout-all")]
        public async Task<IActionResult> LogoutAll()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "User not authenticated" });

            BlacklistCurrentAccessToken();

            try
            {
                var actorRole = User.GetActorRole() ?? Roles.FallbackUnknown;
                await _posShiftService.AutoCloseShiftAsync(userId, actorRole);
            }
            catch (Exception shiftCloseEx)
            {
                _logger.LogWarning(
                    shiftCloseEx,
                    "Auto-close cashier shift failed for user: {UserId} during logout-all, continuing",
                    userId);
            }

            await _refreshTokenService.LogoutAllAsync(userId, "logout_all");
            await TryLogLogoutAuditAsync(userId, reason: "logout_all");
            return Ok(new { message = "All sessions invalidated" });
        }

        [Authorize]
        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke([FromBody] RefreshTokenModel model)
        {
            if (string.IsNullOrWhiteSpace(model.RefreshToken))
                return BadRequest(new { message = "Refresh token is required" });

            var revoked = await _refreshTokenService.RevokeRefreshTokenAsync(model.RefreshToken, "manual_revoke");
            if (!revoked)
                return NotFound(new { message = "Refresh token not found" });
            return Ok(new { message = "Refresh token revoked" });
        }

        private void BlacklistCurrentAccessToken()
        {
            var authHeader = Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(authHeader))
                return;

            var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader[7..].Trim()
                : authHeader.Trim();
            if (string.IsNullOrEmpty(token))
                return;

            var expiry = DateTime.UtcNow.AddMinutes(Math.Max(1, _authOptions.AccessTokenLifetimeMinutes));
            var expRaw = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value
                ?? User.FindFirst("exp")?.Value;
            if (long.TryParse(expRaw, out var expUnix))
            {
                expiry = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
            }

            _tokenBlacklistService.BlacklistToken(token, expiry);
        }

        private async Task TryLogLoginAuditAsync(
            AuditEventType actionType,
            string actorUserId,
            string actorRole,
            Guid? tenantId,
            string description,
            AuditLogStatus status,
            string? reason = null)
        {
            try
            {
                await _auditLogService.LogUserLifecycleAsync(
                    actionType,
                    actorUserId,
                    actorRole,
                    actorUserId,
                    tenantId,
                    reason: reason,
                    description: description,
                    status: status).ConfigureAwait(false);
            }
            catch (Exception auditEx)
            {
                _logger.LogWarning(
                    auditEx,
                    "Failed to write login audit {ActionType} for user {UserId}",
                    actionType,
                    actorUserId);
            }
        }

        private async Task TryLogLogoutAuditAsync(string userId, string reason)
        {
            try
            {
                Guid? tenantId = null;
                var tenantRaw = User.FindFirst("tenant_id")?.Value;
                if (Guid.TryParse(tenantRaw, out var parsedTenant))
                    tenantId = parsedTenant;

                var actorRole = User.GetActorRole() ?? Roles.FallbackUnknown;
                await _auditLogService.LogUserLifecycleAsync(
                    AuditEventType.UserLogout,
                    userId,
                    actorRole,
                    userId,
                    tenantId,
                    reason: reason,
                    description: $"User logout ({reason})");
            }
            catch (Exception auditEx)
            {
                _logger.LogWarning(auditEx, "Failed to write logout audit for user {UserId}", userId);
            }
        }

        private SessionClientMetadata BuildSessionClientMetadata()
        {
            var deviceId = Request.Headers["X-Device-Id"].FirstOrDefault();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.FirstOrDefault();
            return new SessionClientMetadata(deviceId, ip, userAgent);
        }

        private string GenerateJwtToken(IReadOnlyList<Claim> baseClaims, string jti, Guid sessionId, DateTime expiresAtUtc)
        {
            var secretKey = _configuration["JwtSettings:SecretKey"]!;
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = baseClaims.ToList();
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
            claims.Add(new Claim("sid", sessionId.ToString()));

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                claims: claims,
                expires: expiresAtUtc,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return "unknown";

            var atIndex = email.IndexOf('@');
            if (atIndex <= 1)
                return "***";

            var prefix = email[..Math.Min(2, atIndex)];
            var domain = atIndex < email.Length - 1 ? email[atIndex..] : string.Empty;
            return $"{prefix}***{domain}";
        }

        private IActionResult RegistrationFailedResponse() =>
            BadRequest(new
            {
                message = _messages.Get(ApiMessageKeys.RegistrationFailed),
                code = "REGISTRATION_FAILED",
            });

        private static bool IsDuplicateIdentityError(IdentityError error) =>
            string.Equals(error.Code, "DuplicateEmail", StringComparison.Ordinal)
            || string.Equals(error.Code, "DuplicateUserName", StringComparison.Ordinal);

        private async Task<IActionResult> InvalidLoginCredentialsAsync(
            string logReason,
            string? loginMasked = null,
            string? userId = null)
        {
            if (!string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Login failed: {Reason} for user {UserId}", logReason, userId);
            }
            else
            {
                _logger.LogWarning(
                    "Login failed: {Reason} for identifier {LoginMasked}",
                    logReason,
                    loginMasked ?? "unknown");
            }

            await TryLogLoginAuditAsync(
                AuditEventType.LoginFailed,
                actorUserId: string.IsNullOrEmpty(userId) ? "anonymous" : userId,
                actorRole: Roles.FallbackUnknown,
                tenantId: null,
                description: $"Login failed ({logReason})",
                status: AuditLogStatus.Failed,
                reason: logReason).ConfigureAwait(false);

            return Unauthorized(new ApiErrorResponse
            {
                Code = "INVALID_CREDENTIALS",
                Message = _messages.Get(ApiMessageKeys.InvalidLoginCredentials),
            });
        }

        private static string MaskLoginIdentifier(string? loginIdentifier)
        {
            if (string.IsNullOrWhiteSpace(loginIdentifier))
                return "unknown";

            return loginIdentifier.Contains('@', StringComparison.Ordinal)
                ? MaskEmail(loginIdentifier)
                : loginIdentifier.Length <= 2
                    ? "***"
                    : $"{loginIdentifier[..2]}***";
        }

        /// <summary>
        /// Effective permissions for API JSON: same set as embedded in JWT via <see cref="ITokenClaimsService"/>.
        /// Sorted for stable serialization (JWT claim order is not significant for authorization).
        /// </summary>
        private async Task<List<string>> GetEffectivePermissionsListAsync(
            string userId,
            IList<string> roles,
            string? userRoleColumn,
            Guid? tenantId,
            string? appContext,
            CancellationToken cancellationToken)
        {
            var set = await _effectivePermissionResolver.GetEffectivePermissionsAsync(userId, roles, tenantId, cancellationToken);
            var canonicalRoles = TokenClaimsService.CollectCanonicalRoles(roles, userRoleColumn);
            return AdminAppPermissionProfile.FilterToSortedList(appContext, canonicalRoles, set);
        }
    }

    public class RegisterModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string EmployeeNumber { get; set; } = string.Empty;
    }

    public class RefreshTokenModel
    {
        public string RefreshToken { get; set; } = string.Empty;
    }
}
