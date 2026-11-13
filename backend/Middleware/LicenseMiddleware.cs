using System.Net.Mime;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Localization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Tenancy;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Middleware
{
    /// <summary>
    /// Adds license visibility headers and enforces deployment + mandant license policy for authenticated traffic.
    /// Runs after <c>UseAuthentication</c> so JWT <c>app_context</c> is available.
    /// <list type="bullet">
    /// <item>Super Admin: an active <b>system</b> license is sufficient (either layer unlocks FA).</item>
    /// <item>Mandanten-Admin (Manager), Cashier, and POS: an active <b>tenant</b> license is required.
    /// A system/deployment key never unlocks the mandant.</item>
    /// </list>
    /// Mandant lockdown blocks POS operations (<c>LICENSE_LOCKED</c>); FA renewal paths stay available.
    /// Grace period allows POS with <c>X-License-Grace</c> warning headers.
    /// </summary>
    public sealed class LicenseMiddleware
    {
        public const string LicenseStatusHeaderName = "X-License-Status";
        public const string LicenseWarningHeaderName = "X-License-Warning";
        public const string LicenseDaysRemainingHeaderName = "X-License-Days-Remaining";
        public const string LicenseGraceRemainingHeaderName = "X-License-Grace-Remaining";
        public const string LicenseGraceHeaderName = "X-License-Grace";
        public const string LicenseLockedCode = "LICENSE_LOCKED";

        private readonly RequestDelegate _next;

        public LicenseMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            ILicenseService licenseService,
            IUnifiedLicenseService unifiedLicenseService,
            DeploymentLicenseValidator deploymentLicenseValidator,
            ICurrentTenantAccessor tenantAccessor,
            IHostEnvironment environment,
            IOptions<TseOptions> tseOptions,
            IOptions<LicenseOptions> licenseOptions,
            IDevelopmentModeService developmentMode)
        {
            if (LicenseEnforcementPolicy.ShouldDisableEnforcement(
                    environment,
                    tseOptions.Value,
                    developmentMode,
                    licenseOptions.Value))
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

            await licenseService.ValidateAsync(context.RequestAborted).ConfigureAwait(false);
            var deploymentSnapshot = licenseService.GetDeploymentStatus();
            var deploymentStatus = deploymentLicenseValidator.GetStatus(deploymentSnapshot);

            context.Response.OnStarting(() =>
            {
                ApplyHeaders(
                    context,
                    deploymentSnapshot,
                    licenseService.IsLicenseSnapshotInitialized,
                    deploymentStatus);
                return Task.CompletedTask;
            });

            var path = context.Request.Path.Value ?? string.Empty;
            var skipTenantLookup = IsTenantLicensePublicPath(path);
            var tenantId = tenantAccessor.TenantId is Guid tid && tid != Guid.Empty ? tid : (Guid?)null;
            UnifiedLicenseStatusDto? unified = null;
            if (!skipTenantLookup)
            {
                unified = await unifiedLicenseService
                    .GetUnifiedStatusAsync(tenantId, context.RequestAborted)
                    .ConfigureAwait(false);

                if (!IsSuperAdmin(context)
                    && tenantId is not null
                    && IsPosOperation(context)
                    && !unified.IsTenantLicense)
                {
                    await WriteTenantLockedAsync(context, unified.MandantSnapshot, unified).ConfigureAwait(false);
                    return;
                }
            }

            var skipDeploymentLock = unified is not null && unified.IsTenantLicense;
            if (!skipDeploymentLock)
            {
                if (!await TryEnforceDeploymentAccessAsync(
                        context,
                        deploymentSnapshot,
                        deploymentLicenseValidator)
                    .ConfigureAwait(false))
                {
                    return;
                }
            }

            if ((unified is null || unified.IsSystemLicense)
                && !await TryEnforceLicensedFeaturesAsync(context, deploymentSnapshot).ConfigureAwait(false))
            {
                return;
            }

            if (unified?.MandantSnapshot is LicenseStatusInfo mandant)
            {
                var isLocked = mandant.IsLocked || (!mandant.CanAccess && mandant.RequiresRenewal);
                ApplyMandantLicenseHeaders(context, mandant, mandant.IsInGracePeriod && !isLocked);
            }

            await _next(context);
        }

        /// <summary>
        /// Combined gate:
        /// Super Admin may operate when either layer is active (system license is enough);
        /// Mandanten-Admin / Cashier / POS with a tenant context need an active tenant license
        /// (a system key does not unlock the mandant);
        /// platform requests without tenant context need an active system license.
        /// POS lockdown for an expired mandant is handled separately.
        /// </summary>
        public static bool IsLicenseValidForRequest(
            HttpContext context,
            UnifiedLicenseStatusDto status,
            Guid? tenantId)
        {
            if (status is null || !status.AnyLicenseActive)
                return false;

            if (IsSuperAdmin(context))
                return true;

            if (tenantId is Guid id && id != Guid.Empty)
                return status.IsTenantLicense;

            return status.IsSystemLicense;
        }

        private static void ApplyMandantLicenseHeaders(
            HttpContext context,
            LicenseStatusInfo licenseStatus,
            bool isGrace)
        {
            if (context.Response.HasStarted)
                return;

            context.Response.Headers[LicenseStatusHeaderName] = licenseStatus.StatusMessage ?? string.Empty;
            context.Response.Headers[LicenseGraceRemainingHeaderName] =
                licenseStatus.GracePeriodRemaining.ToString();

            if (isGrace)
            {
                context.Response.Headers[LicenseGraceHeaderName] = "true";
                context.Response.Headers[LicenseDaysRemainingHeaderName] =
                    licenseStatus.GracePeriodRemaining.ToString();
            }
            else
            {
                context.Response.Headers[LicenseDaysRemainingHeaderName] =
                    licenseStatus.DaysRemaining.ToString();
            }
        }

        private static async Task WriteTenantLockedAsync(
            HttpContext context,
            LicenseStatusInfo? licenseStatus,
            UnifiedLicenseStatusDto? unified)
        {
            var language = context.Items.TryGetValue(LanguageMiddleware.LanguageItemKey, out var langObj)
                && langObj is string lang
                ? lang
                : LanguageMiddleware.DefaultLanguage;

            var systemActive = unified?.IsSystemLicense == true;
            var tenantLocked = unified is null || !unified.IsTenantLicense;
            var combinedKey = systemActive && tenantLocked
                ? ApiMessageKeys.LicenseStatusSystemActiveTenantLocked
                : ApiMessageKeys.LicenseStatusLocked;
            var message = systemActive && tenantLocked
                ? ApiMessageCatalog.Get(combinedKey, language)
                : licenseStatus is not null && !string.IsNullOrWhiteSpace(licenseStatus.StatusMessage)
                    ? licenseStatus.StatusMessage
                    : ApiMessageCatalog.Get(ApiMessageKeys.LicenseStatusLocked, language);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    success = false,
                    code = LicenseLockedCode,
                    message,
                    messageKey = combinedKey,
                    status = StatusCodes.Status403Forbidden,
                    systemLicense = unified?.SystemLicense,
                    tenantLicense = unified?.TenantLicense,
                    combinedStatus = systemActive && tenantLocked
                        ? "system_active_tenant_locked"
                        : "tenant_locked",
                    licenseStatus = new
                    {
                        expired = true,
                        isLocked = true,
                        isGracePeriod = false,
                        validUntil = licenseStatus?.ValidUntil,
                        daysOverdue = licenseStatus?.DaysOverdue ?? 0,
                        lockDate = licenseStatus?.LockDate,
                        restrictions = licenseStatus?.Restrictions ?? Array.Empty<string>(),
                    },
                },
                context.RequestAborted).ConfigureAwait(false);
        }

        /// <summary>
        /// POS cash-register / fiscal traffic: <c>app_context=pos</c>, <c>/api/pos/*</c>, or fiscal POS paths.
        /// </summary>
        public static bool IsPosOperation(HttpContext context)
        {
            var appContext = LicensePathFeatureEvaluator.ReadAppContext(context);
            if (string.Equals(appContext, ClientAppPolicy.Pos, StringComparison.OrdinalIgnoreCase))
                return true;

            var path = context.Request.Path;
            if (path.StartsWithSegments("/api/pos", StringComparison.OrdinalIgnoreCase))
                return true;

            return LicensePathFeatureEvaluator.IsPosOperationalPath(path);
        }

        private static bool IsSuperAdmin(HttpContext context) =>
            context.User?.IsInRole(Roles.SuperAdmin) == true;

        private static bool IsTenantLicensePublicPath(string path)
        {
            var lower = path.ToLowerInvariant();
            return lower.StartsWith("/api/auth", StringComparison.Ordinal)
                || lower.StartsWith("/api/health", StringComparison.Ordinal)
                || lower.StartsWith("/swagger", StringComparison.Ordinal)
                || lower.StartsWith("/api/license", StringComparison.Ordinal);
        }

        private static async Task<bool> TryEnforceDeploymentAccessAsync(
            HttpContext context,
            LicenseStatusResponse deploymentSnapshot,
            DeploymentLicenseValidator deploymentLicenseValidator)
        {
            if (OpenApiExportMode.IsEnabled)
                return true;

            var deploymentStatus = deploymentLicenseValidator.GetStatus(deploymentSnapshot);
            var permissions = deploymentLicenseValidator.GetPermissions(deploymentSnapshot);
            var path = context.Request.Path;

            if (deploymentStatus == DeploymentLicenseStatus.Lockdown)
            {
                if (IsDeploymentLockdownAllowedPath(path))
                    return true;

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = MediaTypeNames.Application.Json;
                await context.Response.WriteAsync(
                        JsonSerializer.Serialize(
                            new
                            {
                                code = "DEPLOYMENT_LICENSE_LOCKDOWN",
                                message = "Deployment license is locked down. Only health and license activation remain available.",
                            }),
                        context.RequestAborted)
                    .ConfigureAwait(false);
                return false;
            }

            if (!permissions.CanWrite
                && IsWriteMethod(context.Request.Method)
                && !IsDeploymentReadOnlyAllowedWrite(path))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = MediaTypeNames.Application.Json;
                await context.Response.WriteAsync(
                        JsonSerializer.Serialize(
                            new
                            {
                                code = "DEPLOYMENT_LICENSE_READ_ONLY",
                                message = "Deployment license is in read-only mode. Write operations are blocked until renewal.",
                            }),
                        context.RequestAborted)
                    .ConfigureAwait(false);
                return false;
            }

            return true;
        }

        private static async Task<bool> TryEnforceLicensedFeaturesAsync(HttpContext context, LicenseStatusResponse deploymentSnapshot)
        {
            if (OpenApiExportMode.IsEnabled)
                return true;

            var path = context.Request.Path;
            var method = context.Request.Method;
            var required = LicensePathFeatureEvaluator.GetRequiredFeatures(path, method);
            if (required.Count == 0)
                return true;

            var paid = deploymentSnapshot.IsValid && !deploymentSnapshot.IsTrial;
            var trialActive = deploymentSnapshot.IsTrial && !deploymentSnapshot.IsExpired;
            var operational = paid || trialActive;
            if (!operational)
                return true;

            var enabled = deploymentSnapshot.EnabledFeatures ?? LicenseFeatureIds.All;
            var enabledSet = new HashSet<string>(enabled, StringComparer.OrdinalIgnoreCase);
            var appContext = LicensePathFeatureEvaluator.ReadAppContext(context);

            foreach (var featureId in required)
            {
                if (!LicensePathFeatureEvaluator.ShouldEnforceFeature(featureId, appContext))
                    continue;
                if (enabledSet.Contains(featureId))
                    continue;

                if (context.Response.HasStarted)
                    return false;

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = MediaTypeNames.Application.Json;
                await context.Response.WriteAsync(
                        JsonSerializer.Serialize(
                            new
                            {
                                code = "LICENSE_FEATURE_DENIED",
                                message = "This deployment license does not include the required feature for this operation.",
                                requiredFeature = featureId,
                                appContext,
                            }),
                        context.RequestAborted)
                    .ConfigureAwait(false);
                return false;
            }

            return true;
        }

        private static void ApplyHeaders(
            HttpContext context,
            LicenseStatusResponse deploymentSnapshot,
            bool snapshotInitialized,
            DeploymentLicenseStatus deploymentStatus)
        {
            if (context.Response.HasStarted)
                return;

            var statusToken = ResolveLicenseHeaderStatus(deploymentSnapshot, snapshotInitialized);

            if (!context.Response.Headers.ContainsKey(LicenseStatusHeaderName))
                context.Response.Headers.Append(LicenseStatusHeaderName, statusToken);

            if (context.Response.Headers.ContainsKey(LicenseWarningHeaderName))
                return;

            var warning = deploymentStatus switch
            {
                DeploymentLicenseStatus.GraceWrite =>
                    "Deployment-Lizenz abgelaufen; Schreibzugriffe werden bald eingeschraenkt.",
                DeploymentLicenseStatus.GraceReadOnly =>
                    "Deployment-Lizenz abgelaufen; System ist schreibgeschuetzt.",
                DeploymentLicenseStatus.Lockdown or DeploymentLicenseStatus.NoLicense =>
                    "Deployment-Lizenz abgelaufen; nur Health und Aktivierung sind verfuegbar.",
                _ when deploymentSnapshot.IsTrial =>
                    $"Testmodus - noch {deploymentSnapshot.DaysRemaining} Tage gueltig",
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(warning))
            {
                context.Response.Headers.Append(LicenseWarningHeaderName, warning);
            }
        }

        private static bool IsWriteMethod(string method) =>
            HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method)
            || HttpMethods.IsDelete(method);

        private static bool IsDeploymentReadOnlyAllowedWrite(PathString path)
        {
            var value = path.Value ?? string.Empty;
            return value.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase)
                || value.Equals("/api/license/activate", StringComparison.OrdinalIgnoreCase)
                || value.Equals("/api/license/validate", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDeploymentLockdownAllowedPath(PathString path)
        {
            var value = path.Value ?? string.Empty;
            return value.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase)
                || value.Equals("/api/license/activate", StringComparison.OrdinalIgnoreCase)
                || value.Equals("/api/license/validate", StringComparison.OrdinalIgnoreCase)
                || value.Equals("/api/license/info", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Maps snapshot to the public header token (Valid / Trial / Expired / None).</summary>
        public static string ResolveLicenseHeaderStatus(LicenseStatusResponse snapshot, bool snapshotInitialized)
        {
            if (!snapshotInitialized)
                return "None";
            if (snapshot.IsValid)
                return "Valid";
            if (snapshot.IsTrial)
                return "Trial";
            return "Expired";
        }
    }
}
