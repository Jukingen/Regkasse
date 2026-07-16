using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using KasseAPI_Final.Auth;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Auth;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Localization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Localization;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;

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
            IPosShiftService posShiftService)
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
                    return InvalidLoginCredentials(
                        "User not found",
                        loginMasked: MaskLoginIdentifier(loginIdentifier));
                }

                if (!user.IsActive)
                {
                    return InvalidLoginCredentials("Inactive user", userId: user.Id);
                }

                var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
                if (!passwordValid)
                {
                    return InvalidLoginCredentials("Invalid password", userId: user.Id);
                }

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

                // Persist last login for audit and UI (Users list / detail).
                user.LastLoginAt = DateTime.UtcNow;
                user.LoginCount++;
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    _logger.LogWarning("LastLoginAt/LoginCount update failed for user {UserId}, login will continue. Errors: {Errors}",
                        user.Id, string.Join("; ", updateResult.Errors.Select(e => e.Description)));
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
                    expiresIn = Math.Max(60, _authOptions.AccessTokenLifetimeMinutes * 60),
                    refreshToken = issuedTokens.RefreshToken,
                    refreshTokenExpiresAtUtc = issuedTokens.RefreshTokenExpiresAtUtc,
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
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for user: {LoginMasked}", MaskLoginIdentifier(loginIdentifier));
                return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.LoginError) });
            }
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("Logout requested for user: {UserId}", userId);
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

                _logger.LogInformation("Logout successful for user: {UserId}", userId);
                return Ok(new { message = "Logout successful" });
            }
            catch (Exception ex)
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
                _logger.LogError(ex, "Logout error for user: {UserId}. Exception: {ExceptionType}, Message: {ExceptionMessage}", 
                    userId, ex.GetType().Name, ex.Message);
                
                return StatusCode(500, new { message = "Logout failed due to a server error." });
            }
        }

        /// <summary>GET /me — returns current user; 401 if token missing/invalid or no user id claims.</summary>
        [Authorize]
        [HttpGet("me")]
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

        // 🔄 REFRESH TOKEN - Token süresi dolduğunda yenileme
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenModel model)
        {
            try
            {
                _logger.LogInformation("Refresh token request received");

                if (string.IsNullOrWhiteSpace(model.RefreshToken))
                {
                    return BadRequest(new { message = "Refresh token is required" });
                }

                var refreshCt = HttpContext?.RequestAborted ?? CancellationToken.None;
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
                    });

                if (!result.Success || result.Tokens == null)
                {
                    var code = result.ReuseDetected ? "refresh_token_reuse_detected" : result.ErrorCode;
                    return Unauthorized(new { message = "Refresh failed", code });
                }

                return Ok(new
                {
                    token = result.Tokens.AccessToken,
                    expiresIn = Math.Max(60, _authOptions.AccessTokenLifetimeMinutes * 60),
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

        private IActionResult InvalidLoginCredentials(string logReason, string? loginMasked = null, string? userId = null)
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
