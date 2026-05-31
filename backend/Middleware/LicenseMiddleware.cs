using System.Net.Mime;
using System.Text.Json;
using KasseAPI_Final;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;

namespace KasseAPI_Final.Middleware
{
    /// <summary>
    /// Adds license visibility headers and enforces deployment + mandant license policy for authenticated traffic.
    /// Runs after <c>UseAuthentication</c> so JWT <c>app_context</c> is available.
    /// </summary>
    public sealed class LicenseMiddleware
    {
        public const string LicenseStatusHeaderName = "X-License-Status";
        public const string LicenseWarningHeaderName = "X-License-Warning";
        public const string LicenseDaysRemainingHeaderName = "X-License-Days-Remaining";
        public const string LicenseGraceRemainingHeaderName = "X-License-Grace-Remaining";

        private readonly RequestDelegate _next;

        public LicenseMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            ILicenseService licenseService,
            DeploymentLicenseValidator deploymentLicenseValidator,
            ICurrentTenantAccessor tenantAccessor)
        {
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

            if (!await TryEnforceDeploymentAccessAsync(
                    context,
                    deploymentSnapshot,
                    deploymentLicenseValidator)
                .ConfigureAwait(false))
            {
                return;
            }

            if (!await TryEnforceLicensedFeaturesAsync(context, deploymentSnapshot).ConfigureAwait(false))
                return;

            if (!await TryEnforceTenantMandantAccessAsync(context, licenseService, tenantAccessor)
                    .ConfigureAwait(false))
            {
                return;
            }

            await _next(context);
        }

        private static async Task<bool> TryEnforceTenantMandantAccessAsync(
            HttpContext context,
            ILicenseService licenseService,
            ICurrentTenantAccessor tenantAccessor)
        {
            if (OpenApiExportMode.IsEnabled)
                return true;

            if (tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
                return true;

            var path = context.Request.Path.Value ?? string.Empty;
            if (IsTenantLicensePublicPath(path))
                return true;

            var licenseStatus = await licenseService
                .GetLicenseStatusAsync(tenantId, context.RequestAborted)
                .ConfigureAwait(false);

            if (!licenseStatus.CanAccess)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    new
                    {
                        error = "License Expired",
                        message = "Ihre Lizenz ist abgelaufen. Bitte kontaktieren Sie Ihren Administrator.",
                        status = StatusCodes.Status403Forbidden,
                        licenseStatus = new
                        {
                            expired = true,
                            validUntil = licenseStatus.ValidUntil,
                            daysOverdue = licenseStatus.DaysOverdue,
                        },
                    },
                    context.RequestAborted).ConfigureAwait(false);
                return false;
            }

            if (context.Response.HasStarted)
                return true;

            context.Response.Headers[LicenseStatusHeaderName] = licenseStatus.StatusMessage;
            context.Response.Headers[LicenseDaysRemainingHeaderName] = licenseStatus.DaysRemaining.ToString();
            context.Response.Headers[LicenseGraceRemainingHeaderName] = licenseStatus.GracePeriodRemaining.ToString();
            return true;
        }

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
                || value.Equals("/api/license/activate", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDeploymentLockdownAllowedPath(PathString path)
        {
            var value = path.Value ?? string.Empty;
            return value.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase)
                || value.Equals("/api/license/activate", StringComparison.OrdinalIgnoreCase);
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
