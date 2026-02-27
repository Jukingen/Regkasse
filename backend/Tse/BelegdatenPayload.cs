using System.Text.Json.Serialization;

namespace KasseAPI_Final.Tse
{
    /// <summary>
    /// RKSV Belegdaten - Checklist 2: payload deterministik sıralama için kullanılır.
    /// </summary>
    public class BelegdatenPayload
    {
        [JsonPropertyName("kassenId")]
        public string KassenId { get; set; } = string.Empty;

        [JsonPropertyName("belegNr")]
        public string BelegNr { get; set; } = string.Empty;

        [JsonPropertyName("belegDatum")]
        public string BelegDatum { get; set; } = string.Empty; // DD.MM.YYYY

        [JsonPropertyName("uhrzeit")]
        public string Uhrzeit { get; set; } = string.Empty; // HH:MM:SS

        [JsonPropertyName("betrag")]
        public string Betrag { get; set; } = string.Empty;

        [JsonPropertyName("prevSignatureValue")]
        public string PrevSignatureValue { get; set; } = string.Empty;

        [JsonPropertyName("taxDetails")]
        public string TaxDetails { get; set; } = "{}";
    }
}
