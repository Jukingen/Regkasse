using System.Globalization;
using System.Text;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Reports;

public interface ITagesabschlussReportService
{
    /// <summary>RKSV footer using host environment only (Development/Staging → demo).</summary>
    string GetRksvFooter(IHostEnvironment env);

    /// <summary>RKSV footer for Tagesabschluss using full fiscal policy (TSE options + RKSV:Mode).</summary>
    string GetRksvFooterForClosing();

    /// <summary>Short single-line label for API/thermal headers.</summary>
    string GetRksvFooterLabel();

    /// <summary>TSE status badge for report header/footer blocks.</summary>
    string GetTseStatusBadge(bool isSimulated);

    /// <summary>Plain-text RKSV Tagesabschluss report for thermal/POS print.</summary>
    string GenerateReport(
        DailyClosing closing,
        DailyClosingSummaryDto? daySummary = null,
        string? cashierName = null);
}

public sealed class TagesabschlussReportService : ITagesabschlussReportService
{
    private static readonly CultureInfo DeAt = CultureInfo.GetCultureInfo("de-AT");

    private readonly IHostEnvironment _hostEnvironment;
    private readonly TseOptions _tseOptions;
    private readonly IConfiguration _configuration;
    private readonly IRksvEnvironmentService _rksvEnvironment;

    public TagesabschlussReportService(
        IHostEnvironment hostEnvironment,
        IOptions<TseOptions> tseOptions,
        IConfiguration configuration,
        IRksvEnvironmentService rksvEnvironment)
    {
        _hostEnvironment = hostEnvironment;
        _tseOptions = tseOptions.Value;
        _configuration = configuration;
        _rksvEnvironment = rksvEnvironment;
    }

    /// <inheritdoc />
    public string GetRksvFooter(IHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(env);
        return RksvEnvironmentService.FormatFooter(env.IsDevelopment() || env.IsStaging());
    }

    /// <inheritdoc />
    public string GetRksvFooterForClosing() =>
        _rksvEnvironment.GetRksvFooter();

    /// <inheritdoc />
    public string GetRksvFooterLabel() =>
        ResolveClosingIsDemo()
            ? "DEMO / NICHT FISKAL"
            : "RKSV-konform (Registrierkassensicherheitsverordnung)";

    internal static string FormatFooter(bool isDemoFiscal) =>
        RksvEnvironmentService.FormatFooter(isDemoFiscal);

    /// <inheritdoc />
    public string GetTseStatusBadge(bool isSimulated) =>
        FormatTseStatusBadge(isSimulated);

    internal static string FormatTseStatusBadge(bool isSimulated) =>
        isSimulated
            ? "TSE SIMULIERT"
            : "TSE AKTIV";

    /// <inheritdoc />
    public string GenerateReport(
        DailyClosing closing,
        DailyClosingSummaryDto? daySummary = null,
        string? cashierName = null)
    {
        ArgumentNullException.ThrowIfNull(closing);

        var company = _configuration.GetSection("Company").Get<TagesabschlussCompanyConfig>()
                      ?? new TagesabschlussCompanyConfig();
        var footer = _rksvEnvironment.GetRksvFooter();
        var tseStatus = _rksvEnvironment.GetTseStatusDisplay();

        var payments = daySummary?.PaymentBreakdown ?? new PaymentBreakdown();
        if (payments.Total == 0m && daySummary != null)
        {
            payments = PaymentBreakdown.FromAmounts(
                daySummary.TotalCash,
                daySummary.TotalCard,
                daySummary.TotalVoucherRedemptions,
                daySummary.TotalOtherPaymentMethods);
        }

        var tax = daySummary?.TaxBreakdown ?? new DailyClosingTaxBreakdownDto();
        var totalGross = closing.TotalAmount;
        var totalTax = closing.TotalTaxAmount > 0m ? closing.TotalTaxAmount : daySummary?.FiscalTotalTaxAmount ?? 0m;

        var registerLabel = closing.CashRegister?.RegisterNumber
                            ?? closing.CashRegister?.Location
                            ?? closing.CashRegisterId.ToString("N");
        var cashier = cashierName
                      ?? closing.User?.UserName
                      ?? closing.User?.Email
                      ?? closing.UserId;
        var closingLocal = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(closing.ClosingDate);
        var qrPayload = FiscalEnvironmentResolver.BuildClosingQrPayload(
            closing.IsSimulated || _rksvEnvironment.IsDemoMode(),
            closing.TseSignature,
            closingLocal,
            totalGross);

        var builder = new StringBuilder();
        builder.AppendLine("═══════════════════════════════════════════");
        builder.AppendLine($"  {company.Name}");
        builder.AppendLine($"  {company.Address}");
        builder.AppendLine($"  UID: {company.VatId}");
        builder.AppendLine("───────────────────────────────────────────");
        builder.AppendLine($"  TAGESABSCHLUSS - {_rksvEnvironment.GetEnvironmentDisplayName()}");
        builder.AppendLine("───────────────────────────────────────────");
        builder.AppendLine($"  Datum:           {closingLocal:dd.MM.yyyy HH:mm}");
        builder.AppendLine($"  Kasse:           {registerLabel}");
        builder.AppendLine($"  Kassierer:       {cashier}");
        builder.AppendLine($"  {tseStatus}");
        builder.AppendLine("───────────────────────────────────────────");
        builder.AppendLine("  Zahlungsarten:");
        builder.AppendLine($"    Bargeld:       {FormatMoney(payments.Cash)}");
        builder.AppendLine($"    Karte:         {FormatMoney(payments.Card)}");
        builder.AppendLine($"    Gutschein:     {FormatMoney(payments.Voucher)}");
        builder.AppendLine($"    Sonstige:      {FormatMoney(payments.Other)}");
        builder.AppendLine($"    Gesamt:        {FormatMoney(totalGross)}");
        builder.AppendLine("───────────────────────────────────────────");
        builder.AppendLine("  Steuern:");
        builder.AppendLine($"    20%:           {FormatMoney(tax.TaxAt20)}");
        builder.AppendLine($"    10%:           {FormatMoney(tax.TaxAt10)}");
        builder.AppendLine($"    0%:            {FormatMoney(tax.GrossAt0)}");
        builder.AppendLine($"    Gesamt MwSt.:  {FormatMoney(totalTax)}");
        builder.AppendLine("───────────────────────────────────────────");
        builder.AppendLine($"  Transaktionen:   {closing.TransactionCount}");
        builder.AppendLine("───────────────────────────────────────────");
        builder.AppendLine("  Signaturkette:");
        builder.AppendLine($"    Länge:         {closing.SignatureChainLength}");
        builder.AppendLine($"    Vorherige:     {FormatSignaturePreview(closing.PreviousSignature)}");
        builder.AppendLine($"    Aktuelle:      {FormatSignaturePreview(closing.TseSignature)}");
        builder.AppendLine("───────────────────────────────────────────");
        builder.AppendLine("  TSE-Signatur:");
        builder.AppendLine($"  {closing.TseSignature ?? "—"}");
        builder.AppendLine("───────────────────────────────────────────");
        builder.AppendLine($"  QR-Code: {qrPayload}");
        builder.AppendLine(footer);
        builder.AppendLine("═══════════════════════════════════════════");

        return builder.ToString();
    }

    private bool ResolveClosingIsDemo() =>
        FiscalEnvironmentResolver.Resolve(
                _hostEnvironment,
                _tseOptions,
                _configuration,
                rksvEnvironment: _rksvEnvironment)
            .IsDemoFiscal;

    private static string FormatMoney(decimal amount) =>
        amount.ToString("C", DeAt);

    private static string FormatSignaturePreview(string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
            return "—";

        var trimmed = signature.Trim();
        return trimmed.Length <= 20
            ? trimmed
            : $"{trimmed[..20]}...";
    }

    private sealed class TagesabschlussCompanyConfig
    {
        public string Name { get; set; } = "Regkasse Software";

        public string Address { get; set; } = string.Empty;

        public string VatId { get; set; } = string.Empty;
    }
}
