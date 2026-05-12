using System;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;

namespace KasseAPI_Final.Middleware
{
    /// <summary>
    /// Adds license visibility headers to every response without blocking requests when the license is invalid.
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
            // Anonymous auth endpoints must not depend on license evaluation or response header hooks.
            if (context.Request.Path.StartsWithSegments("/api/Auth", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            context.Response.OnStarting(() =>
            {
                ApplyHeaders(context, licenseService);
                return Task.CompletedTask;
            });

            await _next(context);
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
                // HTTP response header values must be ASCII (Kestrel rejects non-Latin-1 / extended chars).
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
