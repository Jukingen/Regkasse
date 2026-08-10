using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.Enums;

/// <summary>Mandanten SaaS package tier on <c>license_sales.license_type</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LicenseType
{
    Trial = 0,
    Starter = 1,
    Business = 2,
    Plus = 3,
}
