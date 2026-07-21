using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// RKDB XML: yalnızca <c>belegpruefung</c> (TEST) — SOAP zarfı burada üretilmez.
/// </summary>
public static class FinanzOnlineRkdbBelegpruefungValidator
{
    private static readonly Regex DepPattern = new(@"^(_[^_]+){13}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>BMF regKasse.xsd beleg: min 100, max 1000, pattern <c>(_ [^_]+){13}</c>.</summary>
    public static IReadOnlyList<string> Validate(FinanzOnlineRkdbBelegpruefungCommand cmd)
    {
        var errors = new List<string>();
        if (cmd == null)
        {
            errors.Add("Command is required.");
            return errors;
        }

        var beleg = (cmd.Beleg ?? "").Trim();
        if (beleg.Length < 100 || beleg.Length > 1000)
            errors.Add("beleg length must be between 100 and 1000 (inclusive).");
        if (!DepPattern.IsMatch(beleg))
            errors.Add("beleg does not match DEP pattern (_segment){13}.");

        if (cmd.PaketNr < 1 || cmd.PaketNr > 999_999_999)
            errors.Add("paket_nr must be between 1 and 999999999.");
        if (cmd.SatzNr < 1 || cmd.SatzNr > 999_999_999)
            errors.Add("satznr must be between 1 and 999999999.");

        if (!string.IsNullOrWhiteSpace(cmd.Kundeninfo) && cmd.Kundeninfo!.Length > 500)
            errors.Add("kundeninfo max length is 500.");

        return errors;
    }

    /// <summary>Receipt/QR kaynağındaki metnin DEP satırı olarak kullanılıp kullanılamayacağını kontrol eder (QR RKS metni genelde farklıdır).</summary>
    public static bool IsValidDepCandidate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var t = text.Trim();
        return t.Length >= 100 && t.Length <= 1000 && DepPattern.IsMatch(t);
    }
}

/// <summary>
/// <c>&lt;rkdb&gt;</c> iç XML üretimi (namespace SOAP ile aynı olmalı).
/// </summary>
public static class FinanzOnlineRkdbBelegpruefungXmlBuilder
{
    public static string Build(string xmlNamespace, FinanzOnlineRkdbBelegpruefungCommand cmd)
    {
        var ns = XNamespace.Get(xmlNamespace.Trim());
        var ts = (cmd.TsErstellungUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var tsStr = ts.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        var belegpruefung = new XElement(ns + "belegpruefung",
            new XElement(ns + "satznr", cmd.SatzNr));
        if (!string.IsNullOrWhiteSpace(cmd.Kundeninfo))
            belegpruefung.Add(new XElement(ns + "kundeninfo", cmd.Kundeninfo!.Trim()));
        belegpruefung.Add(new XElement(ns + "beleg", cmd.Beleg.Trim()));

        var rkdb = new XElement(ns + "rkdb",
            new XElement(ns + "paket_nr", cmd.PaketNr),
            new XElement(ns + "ts_erstellung", tsStr),
            belegpruefung);

        return rkdb.ToString(SaveOptions.DisableFormatting);
    }
}
