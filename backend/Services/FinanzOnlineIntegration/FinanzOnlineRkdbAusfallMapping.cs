using System.Globalization;
using System.Xml.Linq;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>Inner rkdb command for ausfall_* / wiederinbetriebnahme_* (one operation per packet).</summary>
public sealed class FinanzOnlineRkdbAusfallCommand
{
    public int PaketNr { get; set; } = 1;
    public int SatzNr { get; set; } = 1;
    public DateTimeOffset? TsErstellungUtc { get; set; }
    public string? Kundeninfo { get; set; }

    /// <summary><see cref="RksvAusfallEpisodeTypes"/>.</summary>
    public string EpisodeType { get; set; } = RksvAusfallEpisodeTypes.Scu;

    /// <summary><see cref="RksvAusfallOperationKinds"/>.</summary>
    public string OperationKind { get; set; } = RksvAusfallOperationKinds.Ausfall;

    /// <summary>When true, emit <c>ausserbetriebnahme</c> instead of <c>ausfall</c>.</summary>
    public bool IsAusserbetriebnahme { get; set; }

    public string? CertificateSerial { get; set; }
    public string? KassenIdentifikationsnummer { get; set; }
    public string Begruendung { get; set; } = RksvAusfallBegruendungCodes.Other;
    public DateTimeOffset? BeginnAusfallUtc { get; set; }
    public DateTimeOffset? EndeAusfallUtc { get; set; }
}

public static class FinanzOnlineRkdbAusfallValidator
{
    public static IReadOnlyList<string> Validate(FinanzOnlineRkdbAusfallCommand cmd)
    {
        var errors = new List<string>();
        if (cmd == null)
        {
            errors.Add("Command is required.");
            return errors;
        }

        if (cmd.PaketNr < 1 || cmd.PaketNr > 999_999_999)
            errors.Add("paket_nr must be between 1 and 999999999.");
        if (cmd.SatzNr < 1 || cmd.SatzNr > 999_999_999)
            errors.Add("satznr must be between 1 and 999999999.");

        var isScu = string.Equals(cmd.EpisodeType, RksvAusfallEpisodeTypes.Scu, StringComparison.OrdinalIgnoreCase);
        var isKasse = string.Equals(cmd.EpisodeType, RksvAusfallEpisodeTypes.Kasse, StringComparison.OrdinalIgnoreCase);
        if (!isScu && !isKasse)
            errors.Add("EpisodeType must be SCU or Kasse.");

        if (isScu && string.IsNullOrWhiteSpace(cmd.CertificateSerial))
            errors.Add("zertifikatsseriennummer is required for SCU episodes.");
        if (isKasse && string.IsNullOrWhiteSpace(cmd.KassenIdentifikationsnummer))
            errors.Add("kassenidentifikationsnummer is required for Kasse episodes.");

        if (string.IsNullOrWhiteSpace(cmd.Begruendung) || cmd.Begruendung.Trim().Length > 200)
            errors.Add("begruendung is required (max 200).");

        var isWieder = string.Equals(cmd.OperationKind, RksvAusfallOperationKinds.Wiederinbetriebnahme, StringComparison.OrdinalIgnoreCase);
        if (isWieder)
        {
            if (cmd.EndeAusfallUtc is null)
                errors.Add("ende_ausfall is required for Wiederinbetriebnahme.");
            else if (cmd.EndeAusfallUtc > DateTimeOffset.UtcNow.AddMinutes(5))
                errors.Add("ende_ausfall must not be far in the future.");
        }
        else
        {
            if (cmd.BeginnAusfallUtc is null)
                errors.Add("beginn_ausfall is required for Ausfall.");
            else if (cmd.BeginnAusfallUtc > DateTimeOffset.UtcNow.AddMinutes(5))
                errors.Add("beginn_ausfall must not be in the future.");
        }

        if (!string.IsNullOrWhiteSpace(cmd.Kundeninfo) && cmd.Kundeninfo!.Length > 500)
            errors.Add("kundeninfo max length is 500.");

        return errors;
    }
}

/// <summary>Builds &lt;rkdb&gt; inner XML for Ausfall / Wiederinbetriebnahme (SOAP envelope is separate).</summary>
public static class FinanzOnlineRkdbAusfallXmlBuilder
{
    public static string Build(string xmlNamespace, FinanzOnlineRkdbAusfallCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        var ns = XNamespace.Get(xmlNamespace.Trim());
        var ts = (cmd.TsErstellungUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var tsStr = FormatBmfDateTime(ts);

        var isWieder = string.Equals(cmd.OperationKind, RksvAusfallOperationKinds.Wiederinbetriebnahme, StringComparison.OrdinalIgnoreCase);
        var isScu = string.Equals(cmd.EpisodeType, RksvAusfallEpisodeTypes.Scu, StringComparison.OrdinalIgnoreCase);

        XElement operation;
        if (isWieder)
        {
            var elementName = isScu ? "wiederinbetriebnahme_se" : "wiederinbetriebnahme_kasse";
            operation = new XElement(ns + elementName,
                new XElement(ns + "satznr", cmd.SatzNr));
            if (!string.IsNullOrWhiteSpace(cmd.Kundeninfo))
                operation.Add(new XElement(ns + "kundeninfo", cmd.Kundeninfo.Trim()));
            if (isScu)
                operation.Add(new XElement(ns + "zertifikatsseriennummer", cmd.CertificateSerial!.Trim()));
            else
                operation.Add(new XElement(ns + "kassenidentifikationsnummer", cmd.KassenIdentifikationsnummer!.Trim()));
            operation.Add(new XElement(ns + "ende_ausfall", FormatBmfDateTime(cmd.EndeAusfallUtc!.Value.ToUniversalTime())));
        }
        else
        {
            var elementName = isScu ? "ausfall_se" : "ausfall_kasse";
            var innerName = cmd.IsAusserbetriebnahme ? "ausserbetriebnahme" : "ausfall";
            var inner = new XElement(ns + innerName,
                new XElement(ns + "begruendung", cmd.Begruendung.Trim()),
                new XElement(ns + "beginn_ausfall", FormatBmfDateTime(cmd.BeginnAusfallUtc!.Value.ToUniversalTime())));

            operation = new XElement(ns + elementName,
                new XElement(ns + "satznr", cmd.SatzNr));
            if (!string.IsNullOrWhiteSpace(cmd.Kundeninfo))
                operation.Add(new XElement(ns + "kundeninfo", cmd.Kundeninfo.Trim()));
            if (isScu)
                operation.Add(new XElement(ns + "zertifikatsseriennummer", cmd.CertificateSerial!.Trim()));
            else
                operation.Add(new XElement(ns + "kassenidentifikationsnummer", cmd.KassenIdentifikationsnummer!.Trim()));
            operation.Add(inner);
        }

        var rkdb = new XElement(ns + "rkdb",
            new XElement(ns + "paket_nr", cmd.PaketNr),
            new XElement(ns + "ts_erstellung", tsStr),
            operation);

        return rkdb.ToString(SaveOptions.DisableFormatting);
    }

    private static string FormatBmfDateTime(DateTimeOffset utc) =>
        utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
