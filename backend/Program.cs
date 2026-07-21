// .NET 10 hosting: WebApplication / minimal host (no Startup.cs).
// DI, auth, EF, CORS, and the middleware pipeline live in ApplicationHost.CreateWebApplication.
// Health/liveness probes use minimal MapGet endpoints; domain APIs remain controller-based.
// ILicenseService: LicenseServiceRegistration.AddLicenseServices — ProductionLicenseService (singleton in Development, scoped otherwise;
// singleton during OpenAPI export). Concrete LicenseService is always singleton; optional synthetic licensing via IDevelopmentModeService.
// IDevelopmentModeService: singleton in ApplicationHost with DB-backed settings and a 30-second in-memory cache (see DevelopmentModeService).
// NTP: Configure<NtpSettings> + AddScoped<INtpEffectiveSettingsProvider, NtpEffectiveSettingsProvider> in ApplicationHost.
var app = KasseAPI_Final.ApplicationHost.CreateWebApplication(args);
app.Lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("Shutting down gracefully...");
});
await KasseAPI_Final.ApplicationHost.RunAsync(app);
