using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Converters
{
    /// <summary>
    /// JSON serileştirme: string ("standard", "reduced", "special", "zerorate") veya int (1,2,3,4) kabul eder.
    /// ZeroRate: "zerorate", "zero", "0percent". "exempt" deprecated, ZeroRate'e map edilir.
    /// API contract: string enum kullanımı tercih edilir. int girişi deprecated.
    /// </summary>
    public class TaxTypeJsonConverter : JsonConverter<TaxType>
    {
        private static readonly string[] StringToEnum = { "", "standard", "reduced", "special", "zerorate" };

        public override TaxType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    var str = reader.GetString()?.Trim().ToLowerInvariant();
                    return str switch
                    {
                        "standard" => TaxType.Standard,
                        "reduced" => TaxType.Reduced,
                        "special" => TaxType.Special,
                        "zerorate" or "zero" or "0percent" => TaxType.ZeroRate,
                        "exempt" => TaxType.ZeroRate, // Deprecated: geriye dönük uyumluluk
                        _ => TaxType.Standard
                    };
                case JsonTokenType.Number:
                    if (reader.TryGetInt32(out var num))
                    {
                        if (num >= 1 && num <= 4)
                            return (TaxType)num;
                    }
                    return TaxType.Standard;
                default:
                    return TaxType.Standard;
            }
        }

        public override void Write(Utf8JsonWriter writer, TaxType value, JsonSerializerOptions options)
        {
            var idx = (int)value;
            if (idx >= 1 && idx <= 4)
                writer.WriteStringValue(StringToEnum[idx]);
            else
                writer.WriteStringValue("standard");
        }
    }
}
