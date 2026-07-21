using System.Globalization;
using System.Xml.Linq;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Üretilen inner &lt;rkdb&gt; XML için yapısal doğrulama (SOAP zarfı yok). Tam XSD doğrulaması yok — regKasse.xsd ile drift riski.
/// </summary>
public static class FinanzOnlineRkdbInnerXmlStructureValidator
{
    private static readonly string[] BelegpruefungRkdbChildOrder = { "paket_nr", "ts_erstellung", "belegpruefung" };

    /// <summary>Supported inner payload: <c>belegpruefung</c> only (TEST subset).</summary>
    public static IReadOnlyList<string> ValidateBelegpruefungDocument(string? xml, string expectedNamespaceUri)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(xml))
        {
            errors.Add("RKDB inner XML is empty.");
            return errors;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml, LoadOptions.None);
        }
        catch (Exception ex)
        {
            errors.Add($"RKDB XML is not well-formed: {ex.Message}");
            return errors;
        }

        var ns = XNamespace.Get(expectedNamespaceUri.Trim());
        var root = doc.Root;
        if (root == null)
        {
            errors.Add("RKDB XML has no root element.");
            return errors;
        }

        if (!string.Equals(root.Name.LocalName, "rkdb", StringComparison.Ordinal))
            errors.Add($"Expected root local name 'rkdb', got '{root.Name.LocalName}'.");

        if (root.Name.Namespace != ns)
            errors.Add($"Expected rkdb element namespace '{expectedNamespaceUri}', got '{root.Name.NamespaceName}'.");

        var children = root.Elements().ToList();
        var actualOrder = children.Select(c => c.Name.LocalName).ToList();
        if (!actualOrder.SequenceEqual(BelegpruefungRkdbChildOrder, StringComparer.Ordinal))
            errors.Add($"Expected element order under rkdb: {string.Join(", ", BelegpruefungRkdbChildOrder)}; got: {string.Join(", ", actualOrder)}.");

        var allowed = new HashSet<string>(BelegpruefungRkdbChildOrder, StringComparer.Ordinal);
        foreach (var c in children)
        {
            if (!allowed.Contains(c.Name.LocalName))
                errors.Add($"Unexpected element under rkdb: {c.Name.LocalName}.");
        }

        var paket = root.Element(ns + "paket_nr");
        if (paket == null)
            errors.Add("Missing paket_nr.");
        else if (!int.TryParse(paket.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var pak) || pak < 1 || pak > 999_999_999)
            errors.Add("paket_nr must be an integer 1..999999999.");

        var tsEl = root.Element(ns + "ts_erstellung");
        if (tsEl == null)
            errors.Add("Missing ts_erstellung.");
        else if (!FinanzOnlineRkdbXsDateTime.TryParse(tsEl.Value, out _))
            errors.Add("ts_erstellung must be a valid XSD dateTime-compatible string.");

        var bp = root.Element(ns + "belegpruefung");
        if (bp == null)
        {
            errors.Add("Missing belegpruefung.");
            return errors;
        }

        var satz = bp.Element(ns + "satznr");
        if (satz == null)
            errors.Add("belegpruefung: missing satznr.");
        else if (!int.TryParse(satz.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var sn) || sn < 1 || sn > 999_999_999)
            errors.Add("belegpruefung: satznr must be 1..999999999.");

        var kunden = bp.Element(ns + "kundeninfo");
        if (kunden != null && kunden.Value.Trim().Length > 50)
            errors.Add("belegpruefung: kundeninfo max length is 50 (XSD).");

        var belegEl = bp.Element(ns + "beleg");
        if (belegEl == null)
            errors.Add("belegpruefung: missing beleg.");
        else
        {
            var pakV = paket != null && int.TryParse(paket.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var p) ? p : 0;
            var snV = satz != null && int.TryParse(satz.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s : 0;
            var cmd = new FinanzOnlineRkdbBelegpruefungCommand
            {
                Beleg = belegEl.Value,
                PaketNr = pakV,
                SatzNr = snV,
                Kundeninfo = kunden?.Value
            };
            foreach (var e in FinanzOnlineRkdbBelegpruefungValidator.Validate(cmd))
                errors.Add(e);
        }

        return errors;
    }
}
