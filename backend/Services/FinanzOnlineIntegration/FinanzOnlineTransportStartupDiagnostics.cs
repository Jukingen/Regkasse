using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Türkçe: Uygulama açılışında FinanzOnline taşıma / outbox / PROD kesiti için tek yapılandırılmış log (URL, şifre, token içeriği yok).
/// </summary>
public static class FinanzOnlineTransportStartupDiagnostics
{
    /// <summary>
    /// Tek satırda anlaşılır özet: simülasyon bayrakları, outbox, SOAP TEST/PROD uygunluğu (cutover), şirket bağlantı kaynağı.
    /// </summary>
    public static void LogTransportModesAtStartup(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>()
            .CreateLogger("FinanzOnline.TransportStartup");

        var session = sp.GetRequiredService<IOptionsMonitor<FinanzOnlineSessionOptions>>().CurrentValue;
        var reg = sp.GetRequiredService<IOptionsMonitor<FinanzOnlineRegistrierkassenOptions>>().CurrentValue;
        var tx = sp.GetRequiredService<IOptionsMonitor<FinanzOnlineTransmissionQueryOptions>>().CurrentValue;
        var outbox = sp.GetRequiredService<IOptionsMonitor<FinanzOnlineOutboxOptions>>().CurrentValue;
        var cutover = sp.GetRequiredService<IOptionsMonitor<FinanzOnlineCutoverGuardOptions>>().CurrentValue;
        var connectivity = sp.GetRequiredService<IOptionsMonitor<FinanzOnlineConnectivityOptions>>().CurrentValue;
        var devTest = sp.GetRequiredService<IOptionsMonitor<FinanzOnlineDevTestOptions>>().CurrentValue;

        var prodSoapEligible = cutover.AllowProdMode &&
                               (!cutover.RequireExplicitProdApproval ||
                                !string.IsNullOrWhiteSpace(cutover.ProdApprovalToken));
        var soapModes = prodSoapEligible ? "TEST_AND_PROD" : "TEST_ONLY";

        // Yapılandırılmış alanlar: şablon içindeki {Name} anahtarları log sağlayıcısına özellik olarak gider (şifre/token değeri yok).
        logger.LogInformation(
            "FinanzOnline startup snapshot: SessionUseSimulation={SessionUseSimulation} RegistrierkassenUseSimulation={RegistrierkassenUseSimulation} RegistrierkassenEnableRealTestSubmission={RegistrierkassenEnableRealTestSubmission} TransmissionQueryUseSimulation={TransmissionQueryUseSimulation} TransmissionQueryEnableRealTestQuery={TransmissionQueryEnableRealTestQuery} OutboxEnabled={OutboxEnabled} SoapModes={SoapModes} CutoverAllowProdMode={CutoverAllowProdMode} CutoverRequireExplicitProdApproval={CutoverRequireExplicitProdApproval} CutoverProdApprovalConfigured={CutoverProdApprovalConfigured} ConnectivityUseCompanySettings={ConnectivityUseCompanySettings} DevTestAllowEnqueueSmokeTest={DevTestAllowEnqueueSmokeTest}",
            session.UseSimulation,
            reg.UseSimulation,
            reg.EnableRealTestSubmission,
            tx.UseSimulation,
            tx.EnableRealTestQuery,
            outbox.Enabled,
            soapModes,
            cutover.AllowProdMode,
            cutover.RequireExplicitProdApproval,
            !string.IsNullOrWhiteSpace(cutover.ProdApprovalToken),
            connectivity.UseCompanySettings,
            devTest.AllowEnqueueSmokeTest);
    }
}
