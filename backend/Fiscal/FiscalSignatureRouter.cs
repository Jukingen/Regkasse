using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Countries.EInvoicing;
using KasseAPI_Final.Services.Countries.QrRechnung;
using KasseAPI_Final.Services.Countries.Strategies.EuDefault;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.KassenSicherheit;
using KasseAPI_Final.Services.Countries.Strategies.Germany;
using KasseAPI_Final.Services.Countries.Strategies.Switzerland;
using KasseAPI_Final.Services.Countries.Vat;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Fiscal;

public sealed record FiscalSignatureContext(
    CountryStrategyBinding Binding,
    IReadOnlyList<TaxLineItemInput> LineItems,
    decimal TotalGross,
    string ReceiptNumber,
    string? BuyerName = null,
    string? BuyerVatId = null,
    string? BuyerCountry = null,
    Guid? CashRegisterId = null,
    string? RegisterNumber = null,
    string? PrevSignatureValue = null,
    DateTime? Timestamp = null,
    string? TaxDetailsJson = null,
    IDbContextTransaction? DbTransaction = null,
    string? PaymentMethodRaw = null);

/// <summary>
/// Country signing dispatch. Austria calls <see cref="ITseService"/>; Germany is gated
/// and not signed here. Switzerland has no TSE: MWST plus a QR-bill payload.
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
    decimal TotalVat,
    string? UblXml = null,
    string? PeppolStatus = null,
    string? PrevSignatureValue = null,
    string? CertificateThumbprint = null);

public sealed class ChMwstCanaryRejectedException : InvalidOperationException
{
    public ChMwstCanaryRejectedException()
        : base("CH MWST is limited to the configured canary tenant.")
    {
    }
}

public sealed class FiscalSignatureRouter : IFiscalSignatureRouter
{
    public const string AtProvider = "AT_FISKALY";
    public const string DeProvider = "DE_KASSENSICHERHEIT";
    public const string ChProvider = "CH_MWST";
    public const string EuProvider = "EN_16931";

    private readonly IFeatureFlagService? _featureFlags;
    private readonly ITseService? _tse;
    private readonly IKassenSicherheitService? _kassen;
    private readonly DeReceiptPayloadMapper _deReceipt;
    private readonly SwitzerlandTaxStrategy _tax;
    private readonly IQrRechnungBuilder _qr;
    private readonly MwstOptions? _mwst;
    private readonly IEn16931XmlBuilder _ubl;
    private readonly IPeppolSubmissionService _peppol;
    private readonly EuDefaultTaxStrategy _euTax;

    public FiscalSignatureRouter(
        IFeatureFlagService? featureFlags,
        SwitzerlandTaxStrategy? tax = null,
        IQrRechnungBuilder? qr = null,
        IOptions<MwstOptions>? mwst = null,
        IEn16931XmlBuilder? ubl = null,
        IPeppolSubmissionService? peppol = null,
        ITseService? tse = null,
        IKassenSicherheitService? kassen = null,
        DeReceiptPayloadMapper? deReceipt = null)
    {
        _featureFlags = featureFlags;
        _tse = tse;
        _kassen = kassen;
        _deReceipt = deReceipt ?? new DeReceiptPayloadMapper(
            new GermanyTaxStrategy(
                new CountryTaxTypeRegistry(),
                new VatIdValidator(new DisabledViesClient()),
                featureFlags));
        _mwst = mwst?.Value;
        _ubl = ubl ?? new En16931UblXmlBuilder(featureFlags);
        _euTax = new EuDefaultTaxStrategy(
            new CountryProfileRegistry(),
            new VatIdValidator(new DisabledViesClient()),
            featureFlags);
        var peppolOptions = Options.Create(new PeppolOptions());
        _peppol = peppol ?? new PeppolSubmissionService(
            _ubl,
            peppolOptions,
            new MockPeppolAccessPointClient(),
            new HostedPeppolAccessPointClient(peppolOptions, new HttpClient()),
            new InMemoryPeppolSubmissionStore());
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
        if (context.Binding.Profile.Code == CountryProfileCodes.EuDefault)
            return await SignEuAsync(context, cancellationToken).ConfigureAwait(false);

        if (context.Binding.Profile.FiscalSystem == FiscalSystem.RKSV_AT)
            return await SignAtAsync(context).ConfigureAwait(false);

        if (context.Binding.Profile.FiscalSystem == FiscalSystem.KASSENSICHERHEIT_DE)
            return await SignDeAsync(context, cancellationToken).ConfigureAwait(false);

        if (context.Binding.Profile.FiscalSystem != FiscalSystem.MWST_CH)
        {
            throw new InvalidOperationException(
                "Only CH MWST and EU_DEFAULT are routed here. Austrian TSE stays on FiscalTseSigning.");
        }

        if (_featureFlags is not null
            && !_featureFlags.IsEnabled(FeatureFlagNames.FiscalMwstCh, context.Binding.Settings.TenantId.ToString("D")))
        {
            throw new FeatureDisabledException(FeatureFlagNames.FiscalMwstCh);
        }

        if (_mwst is not null)
        {
            if (!Guid.TryParse(_mwst.CanaryTenantId, out var canary)
                || canary != context.Binding.Settings.TenantId)
            {
                throw new ChMwstCanaryRejectedException();
            }
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

    private async Task<FiscalSignatureResult> SignAtAsync(FiscalSignatureContext context)
    {
        if (context.CashRegisterId is null || string.IsNullOrEmpty(context.RegisterNumber))
        {
            throw new InvalidOperationException(
                "AT branch requires cashRegisterId and registerNumber");
        }

        if (_tse is null)
        {
            throw new InvalidOperationException("AT TSE service is not configured.");
        }

        var sig = await _tse.CreateInvoiceSignatureAsync(
            context.CashRegisterId.Value,
            context.ReceiptNumber,
            context.TotalGross,
            context.RegisterNumber,
            context.PrevSignatureValue,
            context.Timestamp,
            context.TaxDetailsJson,
            context.DbTransaction).ConfigureAwait(false);

        return new FiscalSignatureResult(
            AtProvider,
            sig.CompactJws,
            SwissQrText: null,
            TotalVat: 0m,
            PrevSignatureValue: sig.PrevSignatureValueUsed,
            CertificateThumbprint: sig.CertificateThumbprint);
    }

    private async Task<FiscalSignatureResult> SignDeAsync(
        FiscalSignatureContext context,
        CancellationToken cancellationToken)
    {
        var tenantId = context.Binding.Settings.TenantId.ToString("D");
        if (_featureFlags is null
            || !_featureFlags.IsEnabled(FeatureFlagNames.FiscalKassenSicherheitDe, tenantId))
        {
            throw new FiscalSigningNotAvailableException(
                FiscalSigningNotAvailableException.DeFlagOff);
        }

        var settings = context.Binding.Settings;
        var tssId = RequireDeId(settings.DeTssId, "KassenSicherheit__TssId");
        var clientId = RequireDeId(settings.DeClientId, "KassenSicherheit__ClientId");
        if (_kassen is null)
            throw new InvalidOperationException("DE KassenSicherheit service is not configured.");

        var transactionId = Guid.NewGuid().ToString("N");
        var started = await _kassen.StartTransactionAsync(
            new KassenSicherheitStartTransactionRequest(
                settings.TenantId,
                tssId,
                clientId,
                transactionId),
            cancellationToken).ConfigureAwait(false);

        using var taxDoc = System.Text.Json.JsonDocument.Parse(
            string.IsNullOrWhiteSpace(context.TaxDetailsJson) ? "{}" : context.TaxDetailsJson);
        var draft = new PaymentDetails
        {
            TotalAmount = context.TotalGross,
            ReceiptNumber = context.ReceiptNumber,
            TaxDetails = taxDoc,
            PaymentMethodRaw = context.PaymentMethodRaw,
        };
        var payload = _deReceipt.FromPayment(draft);
        var finishRevision = (started.TxRevision ?? 1) + 1;
        var finished = await _kassen.FinishTransactionAsync(
            new KassenSicherheitFinishTransactionRequest(
                settings.TenantId,
                tssId,
                clientId,
                started.TransactionId ?? transactionId,
                finishRevision,
                Receipt: payload,
                Belegnummer: payload.Belegnummer),
            cancellationToken).ConfigureAwait(false);

        return new FiscalSignatureResult(
            DeProvider,
            finished.Signature,
            SwissQrText: null,
            TotalVat: 0m);
    }

    private static string RequireDeId(string? fromSettings, string envName)
    {
        if (!string.IsNullOrWhiteSpace(fromSettings))
            return fromSettings.Trim();

        var fromEnv = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        throw new FiscalSigningNotAvailableException(
            FiscalSigningNotAvailableException.DeNotConfigured);
    }

    private async Task<FiscalSignatureResult> SignEuAsync(
        FiscalSignatureContext context,
        CancellationToken cancellationToken)
    {
        var tenantId = context.Binding.Settings.TenantId.ToString("D");
        if (_featureFlags is not null
            && !_featureFlags.IsEnabled(FeatureFlagNames.EInvoicingEn16931, tenantId))
        {
            throw new FeatureDisabledException(FeatureFlagNames.EInvoicingEn16931);
        }

        if (context.Binding.VatRegime == VatRegime.EU_REVERSE_CHARGE
            && string.IsNullOrEmpty(context.BuyerVatId))
            throw new VatIdShapeInvalidException();

        var tax = _euTax.CalculateTax(
            context.LineItems,
            new TaxCalculationContext
            {
                CountryProfile = context.Binding.Profile,
                VatRegime = context.Binding.VatRegime,
                TaxExempt = context.Binding.Settings.TaxExempt,
                BuyerVatId = context.BuyerVatId,
            });

        var settings = context.Binding.Settings;
        var sellerCountry = settings.Country is { Length: 2 } code
            ? code.ToUpperInvariant()
            : "DE";
        var category = tax.Totals.TotalVat == 0m ? "AE" : "S";
        var net = context.TotalGross - tax.Totals.TotalVat;
        var document = new InvoiceDocumentDto
        {
            CountryCode = CountryProfileCodes.EuDefault,
            SellerName = settings.CompanyName,
            SellerVatId = settings.VatId,
            SellerCountry = sellerCountry,
            BuyerName = string.IsNullOrWhiteSpace(context.BuyerName) ? "EU buyer" : context.BuyerName,
            BuyerVatId = context.BuyerVatId,
            BuyerCountry = string.IsNullOrWhiteSpace(context.BuyerCountry) ? "DE" : context.BuyerCountry,
            InvoiceNumber = context.ReceiptNumber,
            InvoiceDate = DateTime.UtcNow,
            Currency = string.IsNullOrWhiteSpace(settings.Currency) ? "EUR" : settings.Currency,
            NetAmount = net,
            TaxAmount = tax.Totals.TotalVat,
            GrossAmount = context.TotalGross,
            VatCategory = category,
            VatPercent = net == 0m ? 0m : decimal.Round(tax.Totals.TotalVat / net * 100m, 2),
            TaxExemptionReason = category == "AE" ? "Reverse charge" : null,
            TaxExemptionReasonCode = category == "AE" ? "VATEX-EU-AE" : null,
        };

        var submission = await _peppol.SubmitAsync(
            settings.TenantId,
            document,
            context.BuyerVatId,
            cancellationToken).ConfigureAwait(false);

        if (submission.Status == PeppolSubmissionStatus.Failed)
            throw new InvalidOperationException(submission.Detail ?? "EN 16931 submission failed.");

        return new FiscalSignatureResult(
            EuProvider,
            Signature: null,
            SwissQrText: null,
            tax.Totals.TotalVat,
            UblXml: null,
            PeppolStatus: submission.Status.ToString());
    }
}
