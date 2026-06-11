using System.Text.Json.Serialization;

namespace KasseAPI_Final.Tse
{
    /// <summary>
    /// RKSV §9 Abs. 2 Z1–Z7 Belegdaten (Detailspezifikation Abs. 4).
    /// Signed via compressed machine code per Abs. 5 (<see cref="RksvMachineCodeBuilder"/>).
    /// </summary>
    public class BelegdatenPayload
    {
        [JsonPropertyName("Kassen-ID")]
        public string KassenId { get; set; } = string.Empty;

        [JsonPropertyName("Belegnummer")]
        public string Belegnummer { get; set; } = string.Empty;

        /// <summary>ISO 8601 Austria local time without timezone, e.g. 2026-01-15T14:23:55.</summary>
        [JsonPropertyName("Beleg-Datum-Uhrzeit")]
        public string BelegDatumUhrzeit { get; set; } = string.Empty;

        [JsonPropertyName("Betrag-Satz-Normal")]
        public decimal BetragSatzNormal { get; set; }

        [JsonPropertyName("Betrag-Satz-Ermaessigt-1")]
        public decimal BetragSatzErmaessigt1 { get; set; }

        [JsonPropertyName("Betrag-Satz-Ermaessigt-2")]
        public decimal BetragSatzErmaessigt2 { get; set; }

        [JsonPropertyName("Betrag-Satz-Null")]
        public decimal BetragSatzNull { get; set; }

        [JsonPropertyName("Betrag-Satz-Besonders")]
        public decimal BetragSatzBesonders { get; set; }

        [JsonPropertyName("Stand-Umsatz-Zaehler-AES256-ICM")]
        public string StandUmsatzZaehlerAes256Icm { get; set; } = string.Empty;

        [JsonPropertyName("Zertifikat-Seriennummer")]
        public string ZertifikatSeriennummer { get; set; } = string.Empty;

        [JsonPropertyName("Sig-Voriger-Beleg")]
        public string SigVorigerBeleg { get; set; } = string.Empty;
    }
}
