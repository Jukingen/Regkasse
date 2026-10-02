using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProductImeiStatus
{
    InStock = 0,
    Sold = 1,
    Returned = 2,
}
