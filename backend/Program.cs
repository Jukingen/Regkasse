// All DI is configured in ApplicationHost.CreateWebApplication (includes Configure<NtpSettings> and
// AddScoped<INtpEffectiveSettingsProvider, NtpEffectiveSettingsProvider> for NTP fiscal guard / time sync).
// ILicenseService: LicenseServiceRegistration.AddLicenseServices — ProductionLicenseService (singleton in Development, scoped otherwise;
// singleton during OpenAPI export). Concrete LicenseService is always singleton; optional synthetic licensing via IDevelopmentModeService.
// IDevelopmentModeService: singleton in ApplicationHost with DB-backed settings and a 30-second in-memory cache (see DevelopmentModeService).
var app = KasseAPI_Final.ApplicationHost.CreateWebApplication(args);
app.Lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("Shutting down gracefully...");
});
await KasseAPI_Final.ApplicationHost.RunAsync(app);
