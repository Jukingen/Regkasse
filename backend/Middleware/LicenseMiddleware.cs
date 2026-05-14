using System.Net.Mime;
using System.Text.Json;
using KasseAPI_Final;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;

namespace KasseAPI_Final.Middleware
{
    /// <summary>
    /// Adds license visibility headers and enforces per-route license feature flags for authenticated traffic.
    /// Runs after <c>UseAuthentication</c> so JWT <c>app_context</c> is available.
    /// </summary>
    public sealed class LicenseMiddleware
    {
        public const string LicenseStatusHeaderName = "X-License-Status";
        public const string LicenseWarningHeaderName = "X-License-Warning";

        private readonly RequestDelegate _next;

        public LicenseMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ILicenseService licenseService)
        {
            if (context.Request.Path.StartsWithSegments("/api/Auth", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            await licenseService.ValidateAsync(context.RequestAborted).ConfigureAwait(false);

            context.Response.OnStarting(() =>
            {
                ApplyHeaders(context, licenseService);
                return Task.CompletedTask;
            });

            if (!await TryEnforceLicensedFeaturesAsync(context, licenseService).ConfigureAwait(false))
                return;

            await _next(context);
        }

        private static async Task<bool> TryEnforceLicensedFeaturesAsync(HttpContext context, ILicenseService licenseService)
        {
            if (OpenApiExportMode.IsEnabled)
                return true;

            var path = context.Request.Path;
            var method = context.Request.Method;
            var required = LicensePathFeatureEvaluator.GetRequiredFeatures(path, method);
            if (required.Count == 0)
                return true;

            var snapshot = licenseService.GetStatus();
            var paid = snapshot.IsValid && !snapshot.IsTrial;
            var trialActive = snapshot.IsTrial && !snapshot.IsExpired;
            var operational = paid || trialActive;
            if (!operational)
                return true;

            var enabled = snapshot.EnabledFeatures ?? LicenseFeatureIds.All;
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

        private static void ApplyHeaders(HttpContext context, ILicenseService licenseService)
        {
            if (context.Response.HasStarted)
                return;

            var snapshot = licenseService.GetStatus();
            var initialized = licenseService.IsLicenseSnapshotInitialized;
            var statusToken = ResolveLicenseHeaderStatus(snapshot, initialized);

            if (!context.Response.Headers.ContainsKey(LicenseStatusHeaderName))
                context.Response.Headers.Append(LicenseStatusHeaderName, statusToken);

            if (snapshot.IsTrial && !context.Response.Headers.ContainsKey(LicenseWarningHeaderName))
            {
                context.Response.Headers.Append(
                    LicenseWarningHeaderName,
                    $"Testmodus - noch {snapshot.DaysRemaining} Tage gueltig");
            }
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
