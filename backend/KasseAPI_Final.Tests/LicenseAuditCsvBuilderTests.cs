using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.License;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseAuditCsvBuilderTests
{
    [Fact]
    public void Build_IncludesBomAndHeaderAndEscapesCommas()
    {
        var bytes = LicenseAuditCsvBuilder.Build(
        [
            new LicenseAuditLogItemDto(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc),
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                "Cafe, Linz",
                "LICENSE_ACTIVATED",
                "Expired",
                "Active",
                "Ada Admin",
                "Key REGK-…"),
        ]);

        Assert.True(bytes.Length > 3);
        Assert.Equal(0xEF, bytes[0]);
        var text = System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        Assert.Contains("CreatedAtUtc,TenantId,TenantName,Action", text, StringComparison.Ordinal);
        Assert.Contains("\"Cafe, Linz\"", text, StringComparison.Ordinal);
        Assert.Contains("LICENSE_ACTIVATED", text, StringComparison.Ordinal);
    }
}
