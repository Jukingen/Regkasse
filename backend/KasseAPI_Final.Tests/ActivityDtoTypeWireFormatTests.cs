using System.Text.Json;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using Xunit;

namespace KasseAPI_Final.Tests;

public class ActivityDtoTypeWireFormatTests
{
    private static readonly JsonSerializerOptions ApiJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void Type_SerializesAsMemberName()
    {
        var json = JsonSerializer.Serialize(
            new ActivityDto { Type = ActivityEventType.TenantCountryChanged },
            ApiJson);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("TenantCountryChanged", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void Type_DeserializesMemberName()
    {
        var dto = JsonSerializer.Deserialize<ActivityDto>("""{"type":"QrRechnungPayloadBuilt"}""", ApiJson);

        Assert.NotNull(dto);
        Assert.Equal(ActivityEventType.QrRechnungPayloadBuilt, dto.Type);
    }
}
