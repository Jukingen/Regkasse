using KasseAPI_Final;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace KasseAPI_Final.Services;

/// <summary>
/// Central registration for license evaluation services (<see cref="ILicenseService"/>).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="LicenseService"/> is always registered as a <strong>singleton</strong> because it owns the in-memory
/// license snapshot and must align with <see cref="LicenseComplianceHostedService"/> / <see cref="LicenseReminderHostedService"/>.
/// </para>
/// <para>
/// <see cref="ILicenseService"/> is implemented by <see cref="DevelopmentLicenseService"/> in development (singleton;
/// synthetic licensed snapshot for unblocked local testing) or by <see cref="ProductionLicenseService"/> in non-development
/// hosting (<strong>scoped</strong> per the product contract; hosted background work resolves it via
/// <see cref="Microsoft.Extensions.DependencyInjection.IServiceScopeFactory"/>).
/// </para>
/// <para>
/// When <see cref="OpenApiExportMode.IsEnabled"/> is true, <see cref="ILicenseService"/> is registered as a singleton
/// <see cref="ProductionLicenseService"/> so the interface can be resolved without an HTTP scope during OpenAPI generation.
/// </para>
/// </remarks>
public static class LicenseServiceRegistration
{
    /// <summary>
    /// Registers <see cref="LicenseService"/> and binds <see cref="ILicenseService"/> for the current hosting environment.
    /// </summary>
    /// <param name="services">Application service collection.</param>
    /// <param name="environment">Host environment (development vs production).</param>
    public static void AddLicenseServices(this IServiceCollection services, IWebHostEnvironment environment)
    {
        services.AddSingleton<LicenseService>();

        if (OpenApiExportMode.IsEnabled)
        {
            services.AddSingleton<ILicenseService, ProductionLicenseService>();
            return;
        }

        if (environment.IsDevelopment())
        {
            services.AddSingleton<ILicenseService, DevelopmentLicenseService>();
            return;
        }

        services.AddScoped<ILicenseService, ProductionLicenseService>();
    }
}
