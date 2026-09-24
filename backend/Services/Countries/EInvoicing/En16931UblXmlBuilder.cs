using System.Globalization;
using System.Text;
using System.Xml;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.FeatureFlags;

namespace KasseAPI_Final.Services.Countries.EInvoicing;

/// <summary>
/// EN 16931 invoice in UBL 2.1 (Peppol BIS Billing 3.0 customization).
/// No Peppol send. Flag <c>EInvoicing.En16931</c> gates the build.
/// </summary>
public sealed class En16931UblXmlBuilder : IEn16931XmlBuilder
{
    public const string CustomizationId = "urn:cen.eu:en16931:2017";
    public const string ProfileId = "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0";

    private readonly IFeatureFlagService? _featureFlags;

    public En16931UblXmlBuilder(IFeatureFlagService? featureFlags = null)
    {
        _featureFlags = featureFlags;
    }

    public Task<string> BuildXmlAsync(
        InvoiceDocumentDto document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        if (_featureFlags is not null
            && !_featureFlags.IsEnabled(FeatureFlagNames.EInvoicingEn16931))
        {
            throw new FeatureDisabledException(FeatureFlagNames.EInvoicingEn16931);
        }

        Require(document.InvoiceNumber, "BT-1 Invoice number");
        if (document.InvoiceDate is null)
            throw new ArgumentException("BT-2 Invoice issue date is required.");
        Require(document.Currency, "BT-5 Invoice currency");
        Require(document.SellerName, "BT-27 Seller name");
        Require(document.SellerCountry, "BT-40 Seller country");
        Require(document.BuyerName, "BT-44 Buyer name");
        Require(document.BuyerCountry, "BT-55 Buyer country");
        Require(document.VatCategory, "BT-118 VAT category");

        var currency = document.Currency.Trim().ToUpperInvariant();
        var category = document.VatCategory.Trim().ToUpperInvariant();
        var net = Money(document.NetAmount);
        var tax = Money(document.TaxAmount);
        var gross = Money(document.GrossAmount);
        var percent = document.VatPercent.ToString("0.##", CultureInfo.InvariantCulture);
        var issueDate = document.InvoiceDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var lineName = string.IsNullOrWhiteSpace(document.PerformanceDescription)
            ? "Goods"
            : document.PerformanceDescription.Trim();

        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = false,
            Encoding = new UTF8Encoding(false),
            Indent = false,
        };

        using var buffer = new StringWriter();
        using (var xml = XmlWriter.Create(buffer, settings))
        {
            xml.WriteStartElement("Invoice", "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2");
            xml.WriteAttributeString("xmlns", "cac", null, "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
            xml.WriteAttributeString("xmlns", "cbc", null, "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");

            Cbc(xml, "CustomizationID", CustomizationId);
            Cbc(xml, "ProfileID", ProfileId);
            Cbc(xml, "ID", document.InvoiceNumber!.Trim());
            Cbc(xml, "IssueDate", issueDate);
            Cbc(xml, "InvoiceTypeCode", "380");
            Cbc(xml, "DocumentCurrencyCode", currency);

            Party(xml, "AccountingSupplierParty", document.SellerName!, document.SellerVatId,
                document.SellerStreet, document.SellerCity, document.SellerPostalCode, document.SellerCountry!);
            Party(xml, "AccountingCustomerParty", document.BuyerName!, document.BuyerVatId,
                document.BuyerStreet, document.BuyerCity, document.BuyerPostalCode, document.BuyerCountry!);

            xml.WriteStartElement("cac", "TaxTotal", null);
            Amount(xml, "TaxAmount", currency, tax);
            xml.WriteStartElement("cac", "TaxSubtotal", null);
            Amount(xml, "TaxableAmount", currency, net);
            Amount(xml, "TaxAmount", currency, tax);
            xml.WriteStartElement("cac", "TaxCategory", null);
            Cbc(xml, "ID", category);
            Cbc(xml, "Percent", percent);
            if (!string.Equals(category, "S", StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(document.TaxExemptionReasonCode))
                    Cbc(xml, "TaxExemptionReasonCode", document.TaxExemptionReasonCode.Trim());
                if (!string.IsNullOrWhiteSpace(document.TaxExemptionReason))
                    Cbc(xml, "TaxExemptionReason", document.TaxExemptionReason.Trim());
            }
            xml.WriteStartElement("cac", "TaxScheme", null);
            Cbc(xml, "ID", "VAT");
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();

            xml.WriteStartElement("cac", "LegalMonetaryTotal", null);
            Amount(xml, "LineExtensionAmount", currency, net);
            Amount(xml, "TaxExclusiveAmount", currency, net);
            Amount(xml, "TaxInclusiveAmount", currency, gross);
            Amount(xml, "PayableAmount", currency, gross);
            xml.WriteEndElement();

            xml.WriteStartElement("cac", "InvoiceLine", null);
            Cbc(xml, "ID", "1");
            xml.WriteStartElement("cbc", "InvoicedQuantity", null);
            xml.WriteAttributeString("unitCode", "C62");
            xml.WriteString("1");
            xml.WriteEndElement();
            Amount(xml, "LineExtensionAmount", currency, net);
            xml.WriteStartElement("cac", "Item", null);
            Cbc(xml, "Name", lineName);
            xml.WriteStartElement("cac", "ClassifiedTaxCategory", null);
            Cbc(xml, "ID", category);
            Cbc(xml, "Percent", percent);
            xml.WriteStartElement("cac", "TaxScheme", null);
            Cbc(xml, "ID", "VAT");
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteStartElement("cac", "Price", null);
            Amount(xml, "PriceAmount", currency, net);
            xml.WriteEndElement();
            xml.WriteEndElement();

            xml.WriteEndElement();
        }

        return Task.FromResult(buffer.ToString());
    }

    private static void Party(
        XmlWriter xml,
        string role,
        string name,
        string? vatId,
        string? street,
        string? city,
        string? postal,
        string country)
    {
        xml.WriteStartElement("cac", role, null);
        xml.WriteStartElement("cac", "Party", null);
        xml.WriteStartElement("cac", "PartyName", null);
        Cbc(xml, "Name", name);
        xml.WriteEndElement();
        xml.WriteStartElement("cac", "PostalAddress", null);
        if (!string.IsNullOrWhiteSpace(street))
            Cbc(xml, "StreetName", street.Trim());
        if (!string.IsNullOrWhiteSpace(city))
            Cbc(xml, "CityName", city.Trim());
        if (!string.IsNullOrWhiteSpace(postal))
            Cbc(xml, "PostalZone", postal.Trim());
        xml.WriteStartElement("cac", "Country", null);
        Cbc(xml, "IdentificationCode", country.Trim().ToUpperInvariant());
        xml.WriteEndElement();
        xml.WriteEndElement();
        if (!string.IsNullOrWhiteSpace(vatId))
        {
            xml.WriteStartElement("cac", "PartyTaxScheme", null);
            Cbc(xml, "CompanyID", vatId.Trim());
            xml.WriteStartElement("cac", "TaxScheme", null);
            Cbc(xml, "ID", "VAT");
            xml.WriteEndElement();
            xml.WriteEndElement();
        }
        xml.WriteStartElement("cac", "PartyLegalEntity", null);
        Cbc(xml, "RegistrationName", name);
        xml.WriteEndElement();
        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    private static void Cbc(XmlWriter xml, string name, string value)
    {
        xml.WriteStartElement("cbc", name, null);
        xml.WriteString(value);
        xml.WriteEndElement();
    }

    private static void Amount(XmlWriter xml, string name, string currency, string value)
    {
        xml.WriteStartElement("cbc", name, null);
        xml.WriteAttributeString("currencyID", currency);
        xml.WriteString(value);
        xml.WriteEndElement();
    }

    private static string Money(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private static void Require(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(field + " is required.");
    }
}
