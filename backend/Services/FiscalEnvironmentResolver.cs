using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services.Rksv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Services;

/// <summary>
/// Resolves demo vs production fiscal presentation for RKSV receipts and closings.
/// Aligns with <see cref="ReceiptService"/> RKSV receipt labels and payment QR policy.
/// </summary>
public static class FiscalEnvironmentResolver
{
    public sealed record FiscalEnvironment(
        bool IsDemoFiscal,
        string EnvironmentName,
        string RksvFooterLabel,
        string TseStatusDisplay,
        string TseStatusBadge);

    public static FiscalEnvironment Resolve(
        IHostEnvironment env,
        TseOptions tseOptions,
        IConfiguration? configuration = null,
        RksvOptions? rksvOptions = null,
        IRksvEnvironmentService? rksvEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(tseOptions);

        rksvOptions ??= configuration?.GetSection(RksvOptions.SectionName).Get<RksvOptions>()
                        ?? new RksvOptions();

        var rksvModeDemo = rksvEnvironment?.IsDemoMode() == true
                           || rksvOptions.IsDemoMode
                           || string.Equals(
                               configuration?["RKSV:Mode"],
                               "Demo",
                               StringComparison.OrdinalIgnoreCase);

        var isDemo = env.IsDevelopment()
                     || env.IsStaging()
                     || rksvModeDemo
                     || rksvOptions.IsTseSimulation
                     || tseOptions.IsFakeSigningMode
                     || tseOptions.UseSoftTseWhenNoDevice;

        var showDemoLabel = isDemo
                            && (rksvEnvironment?.ShowDemoLabel() == true
                                || rksvOptions.ShowDemoLabel
                                || env.IsDevelopment()
                                || env.IsStaging());

        var tseSimulated = isDemo
                           || rksvEnvironment?.IsTseSimulated() == true
                           || rksvOptions.IsTseSimulation;

        return new FiscalEnvironment(
            IsDemoFiscal: isDemo,
            EnvironmentName: isDemo ? "Demo" : "Production",
            RksvFooterLabel: showDemoLabel
                ? "DEMO / NICHT FISKAL"
                : "RKSV-konform (Registrierkassensicherheitsverordnung)",
            TseStatusDisplay: tseSimulated
                ? "TSE: SIMULIERT (NUR TEST)"
                : "TSE: AKTIV ✅",
            TseStatusBadge: tseSimulated
                ? "TSE SIMULIERT"
                : "TSE AKTIV");
    }

    public static string BuildClosingQrPayload(
        bool isDemoFiscal,
        string? tseSignature,
        DateTime businessDate,
        decimal totalAmount)
    {
        if (!isDemoFiscal && !string.IsNullOrWhiteSpace(tseSignature))
        {
            if (RksvReceiptQrPayloadBuilder.TryBuildFromCompactJws(tseSignature.Trim(), out var qr))
                return qr;
        }

        var formattedTotal = totalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        return $"NON_FISCAL_DEMO_DAILY_{businessDate:yyyy-MM-dd}_{formattedTotal}";
    }
}
