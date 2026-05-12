// Receipt PDF reprint: IReceiptPdfService → ApplicationHost.CreateWebApplication (builder.Services.AddScoped<...>).
var app = KasseAPI_Final.ApplicationHost.CreateWebApplication(args);
app.Lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("Shutting down gracefully...");
});
await KasseAPI_Final.ApplicationHost.RunAsync(app);
