using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Austria;
using KasseAPI_Final.Services.Countries.Strategies.EuDefault;
using KasseAPI_Final.Services.Countries.Strategies.Germany;
using KasseAPI_Final.Services.Countries.Strategies.Switzerland;
using KasseAPI_Final.Services.Offline;
using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Services.Countries;

/// <summary>
/// Default resolvers for tests and constructor fallbacks. Production DI registers the same types
/// in <c>ApplicationHost</c>.
/// </summary>
internal static class CountryStrategyWiring
{
    public static ITaxStrategyResolver CreateTaxResolver() =>
        new TaxStrategyResolver(
        [
            new AustriaTaxStrategy(),
            new GermanyTaxStrategy(),
            new SwitzerlandTaxStrategy(),
            new EuDefaultTaxStrategy(),
        ]);

    public static IInvoiceStrategyResolver CreateInvoiceResolver(
        ISequenceReservationService? sequences = null,
        IReceiptService? receipts = null) =>
        new InvoiceStrategyResolver(
        [
            new AustriaInvoiceStrategy(sequences!, receipts!),
            new GermanyInvoiceStrategy(),
            new SwitzerlandInvoiceStrategy(),
            new EuDefaultInvoiceStrategy(),
        ]);

    public static RksvTaxSetAmounts RequireTaxSets(RksvTaxSetAmounts? projected, string? countryCode) =>
        projected ?? throw new InvalidOperationException(
            $"ProjectFiscalTaxSets returned null for country '{countryCode}'.");
}
