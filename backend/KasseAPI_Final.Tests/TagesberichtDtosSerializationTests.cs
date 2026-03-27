using System.Text.Json;
using KasseAPI_Final.Models.Reports;
using Xunit;

namespace KasseAPI_Final.Tests;

public class TagesberichtDtosSerializationTests
{
    [Fact]
    public void TagesberichtSummaryDto_round_trips_json()
    {
        var dto = new TagesberichtSummaryDto
        {
            SchemaVersion = "1.0",
            GrossSalesAmount = 100.50m,
            TaxTotalAmount = 10m,
            TracePaymentIdsHash = "abc",
        };
        var json = JsonSerializer.Serialize(dto);
        var back = JsonSerializer.Deserialize<TagesberichtSummaryDto>(json);
        Assert.NotNull(back);
        Assert.Equal(100.50m, back!.GrossSalesAmount);
        Assert.Equal("abc", back.TracePaymentIdsHash);
    }
}
