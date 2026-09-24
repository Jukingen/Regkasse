using System.Xml;
using System.Xml.XPath;

namespace KasseAPI_Final.Services.Countries.EInvoicing;

/// <summary>
/// Core EN 16931 business rules (BR-*) as ISO Schematron, XPath 1.0.
/// This is the validator-only subset, not the full KoSIT / Peppol pack.
/// </summary>
public static class En16931Schematron
{
    public const string Rules = """
        <schema xmlns="http://purl.oclc.org/dsdl/schematron" queryBinding="xslt">
          <ns prefix="ubl" uri="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"/>
          <ns prefix="cac" uri="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"/>
          <ns prefix="cbc" uri="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"/>
          <pattern id="en16931-core">
            <rule context="/ubl:Invoice">
              <assert id="BR-01" test="cbc:CustomizationID">An Invoice shall have a Specification identifier (BT-24).</assert>
              <assert id="BR-02" test="normalize-space(cbc:ID) != ''">An Invoice shall have an Invoice number (BT-1).</assert>
              <assert id="BR-03" test="cbc:IssueDate">An Invoice shall have an Invoice issue date (BT-2).</assert>
              <assert id="BR-04" test="cbc:InvoiceTypeCode = '380'">An Invoice shall have the Invoice type code 380 (BT-3).</assert>
              <assert id="BR-05" test="string-length(cbc:DocumentCurrencyCode) = 3">An Invoice shall have an Invoice currency code (BT-5).</assert>
              <assert id="BR-06" test="cac:AccountingSupplierParty/cac:Party/cac:PartyLegalEntity/cbc:RegistrationName">An Invoice shall contain the Seller name (BT-27).</assert>
              <assert id="BR-07" test="cac:AccountingCustomerParty/cac:Party/cac:PartyLegalEntity/cbc:RegistrationName">An Invoice shall contain the Buyer name (BT-44).</assert>
              <assert id="BR-08" test="cac:AccountingSupplierParty/cac:Party/cac:PostalAddress/cac:Country/cbc:IdentificationCode">An Invoice shall contain the Seller country code (BT-40).</assert>
              <assert id="BR-10" test="cac:AccountingCustomerParty/cac:Party/cac:PostalAddress/cac:Country/cbc:IdentificationCode">An Invoice shall contain the Buyer country code (BT-55).</assert>
              <assert id="BR-16" test="cac:InvoiceLine">An Invoice shall have at least one Invoice line (BG-25).</assert>
              <assert id="BR-CO-10" test="number(cac:LegalMonetaryTotal/cbc:LineExtensionAmount) = number(cac:LegalMonetaryTotal/cbc:TaxExclusiveAmount)">Sum of Invoice line net amount (BT-106) shall equal the Invoice total amount without VAT (BT-109).</assert>
              <assert id="BR-CO-15" test="number(cac:LegalMonetaryTotal/cbc:TaxExclusiveAmount) + number(cac:TaxTotal/cbc:TaxAmount) = number(cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount)">Invoice total with VAT (BT-112) shall be the sum of BT-109 and BT-110.</assert>
              <assert id="BR-CO-16" test="number(cac:LegalMonetaryTotal/cbc:PayableAmount) = number(cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount)">Amount due for payment (BT-115) shall equal the Invoice total with VAT (BT-112).</assert>
            </rule>
            <rule context="/ubl:Invoice/cac:InvoiceLine">
              <assert id="BR-21" test="normalize-space(cbc:ID) != ''">Each Invoice line shall have an Invoice line identifier (BT-126).</assert>
              <assert id="BR-22" test="cbc:InvoicedQuantity">Each Invoice line shall have an Invoiced quantity (BT-129).</assert>
              <assert id="BR-24" test="cbc:LineExtensionAmount">Each Invoice line shall have an Invoice line net amount (BT-131).</assert>
              <assert id="BR-25" test="cac:Item/cbc:Name">Each Invoice line shall contain the Item name (BT-153).</assert>
              <assert id="BR-26" test="cac:Price/cbc:PriceAmount">Each Invoice line shall contain the Item net price (BT-146).</assert>
              <assert id="BR-CO-04" test="cac:Item/cac:ClassifiedTaxCategory/cbc:ID">Each Invoice line shall be categorized with an Invoiced item VAT category code (BT-151).</assert>
            </rule>
            <rule context="/ubl:Invoice/cac:TaxTotal/cac:TaxSubtotal">
              <assert id="BR-45" test="cbc:TaxAmount">Each VAT breakdown shall have a VAT category tax amount (BT-117).</assert>
              <assert id="BR-47" test="cac:TaxCategory/cbc:ID">Each VAT breakdown shall be defined through a VAT category code (BT-118).</assert>
              <assert id="BR-48" test="cac:TaxCategory/cbc:Percent">Each VAT breakdown shall have a VAT category rate (BT-119).</assert>
              <assert id="BR-S-01" test="cac:TaxCategory/cbc:ID != 'S' or number(cbc:TaxAmount) = round(number(cbc:TaxableAmount) * number(cac:TaxCategory/cbc:Percent)) div 100">A standard rated VAT category tax amount shall equal taxable amount times rate.</assert>
            </rule>
          </pattern>
        </schema>
        """;

    public static IReadOnlyList<string> Validate(string ublXml)
    {
        var invoice = new XmlDocument { XmlResolver = null };
        invoice.LoadXml(ublXml);

        var rules = new XmlDocument { XmlResolver = null };
        rules.LoadXml(Rules);

        var ns = new XmlNamespaceManager(invoice.NameTable);
        ns.AddNamespace("ubl", "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2");
        ns.AddNamespace("cac", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
        ns.AddNamespace("cbc", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");

        var sch = new XmlNamespaceManager(rules.NameTable);
        sch.AddNamespace("sch", "http://purl.oclc.org/dsdl/schematron");

        var failures = new List<string>();
        foreach (XmlElement rule in rules.SelectNodes("//sch:rule", sch)!)
        {
            var context = rule.GetAttribute("context");
            var nodes = invoice.SelectNodes(context, ns);
            if (nodes is null || nodes.Count == 0)
            {
                if (context.Contains("InvoiceLine", StringComparison.Ordinal)
                    || context.Contains("TaxSubtotal", StringComparison.Ordinal))
                {
                    failures.Add(context + ": no context node");
                }
                continue;
            }

            foreach (XmlNode node in nodes)
            {
                var nav = node.CreateNavigator();
                foreach (XmlElement assert in rule.SelectNodes("sch:assert", sch)!)
                {
                    var test = assert.GetAttribute("test");
                    var id = assert.GetAttribute("id");
                    if (!Passes(nav!, test, ns))
                        failures.Add(id + ": " + assert.InnerText);
                }
            }
        }

        return failures;
    }

    private static bool Passes(XPathNavigator nav, string test, XmlNamespaceManager ns)
    {
        var expr = nav.Compile(test);
        expr.SetContext(ns);
        var result = nav.Evaluate(expr);
        return result switch
        {
            bool flag => flag,
            XPathNodeIterator it => it.Count > 0,
            double number => number != 0d && !double.IsNaN(number),
            string text => text.Length > 0,
            _ => false,
        };
    }
}
