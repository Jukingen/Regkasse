// All DI is configured in ApplicationHost.CreateWebApplication (includes Configure<NtpSettings> and
// AddScoped<INtpEffectiveSettingsProvider, NtpEffectiveSettingsProvider> for NTP fiscal guard / time sync).
// ILicenseService: LicenseServiceRegistration.AddLicenseServices — DevelopmentLicenseService (Development),
// ProductionLicenseService (non-Development, scoped; singleton during OpenAPI export). Concrete LicenseService is always singleton.
var app = KasseAPI_Final.ApplicationHost.CreateWebApplication(args);
app.Lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("Shutting down gracefully...");
});
await KasseAPI_Final.ApplicationHost.RunAsync(app);
