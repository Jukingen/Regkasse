using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.QrRechnung;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Switzerland;
using KasseAPI_Final.Services.Countries.Vat;
using KasseAPI_Final.Services.FeatureFlags;

namespace KasseAPI_Final.Fiscal;

public sealed record FiscalSignatureContext(
    CountryStrategyBinding Binding,
    IReadOnlyList<TaxLineItemInput> LineItems,
    decimal TotalGross,
    string ReceiptNumber);

/// <summary>
/// Country signing dispatch. Austria stays on <see cref="FiscalTseSigning"/>.
/// Switzerland has no TSE: MWST plus a QR-bill payload, signature left empty.
/// </summary>
public interface IFiscalSignatureRouter
{
    Task<FiscalSignatureResult> SignAsync(
        FiscalSignatureContext context,
        CancellationToken cancellationToken = default);
}

public sealed record FiscalSignatureResult(
    string Provider,
    string? Signature,
    string? SwissQrText,
    decimal TotalVat);

public sealed class FiscalSignatureRouter : IFiscalSignatureRouter
{
    public const string ChProvider = "CH_MWST";

    private readonly IFeatureFlagService? _featureFlags;
    private readonly SwitzerlandTaxStrategy _tax;
    private readonly IQrRechnungBuilder _qr;

    public FiscalSignatureRouter(
        IFeatureFlagService? featureFlags,
        SwitzerlandTaxStrategy? tax = null,
        IQrRechnungBuilder? qr = null)
    {
        _featureFlags = featureFlags;
        _tax = tax ?? new SwitzerlandTaxStrategy(
            new CountryTaxTypeRegistry(),
            new VatIdValidator(new DisabledViesClient()),
            featureFlags);
        _qr = qr ?? new QrRechnungBuilder(featureFlags: null);
    }

    public async Task<FiscalSignatureResult> SignAsync(
        FiscalSignatureContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Binding.Profile.FiscalSystem != FiscalSystem.MWST_CH)
        {
            throw new InvalidOperationException(
                "Only CH MWST is routed here. Austrian TSE stays on FiscalTseSigning.");
        }

        if (_featureFlags is not null
            && !_featureFlags.IsEnabled(FeatureFlagNames.FiscalMwstCh, context.Binding.Settings.TenantId.ToString("D")))
        {
            throw new FeatureDisabledException(FeatureFlagNames.FiscalMwstCh);
        }

        var tax = _tax.CalculateTax(
            context.LineItems,
            new TaxCalculationContext
            {
                CountryProfile = context.Binding.Profile,
                VatRegime = context.Binding.VatRegime,
                TaxExempt = context.Binding.Settings.TaxExempt,
            });

        var settings = context.Binding.Settings;
        var payload = await _qr.BuildPayloadAsync(
            new QrRechnungRequest(
                Iban: settings.BankAccountNumber ?? string.Empty,
                Creditor: new QrRechnungParty(
                    settings.CompanyName,
                    settings.CompanyAddress,
                    null,
                    string.Empty,
                    string.Empty,
                    CountryProfileCodes.Switzerland),
                Debtor: null,
                Amount: context.TotalGross,
                Currency: string.IsNullOrWhiteSpace(settings.Currency) ? "CHF" : settings.Currency,
                Reference: null,
                AdditionalInfo: context.ReceiptNumber,
                ReferenceType: QrRechnungReferenceType.Non),
            cancellationToken).ConfigureAwait(false);

        return new FiscalSignatureResult(
            ChProvider,
            Signature: null,
            payload.SwissQrText,
            tax.Totals.TotalVat);
    }
}
