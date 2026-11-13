using System.Text;
using KasseAPI_Final.Models.Enums;
using KasseAPI_Final.Services.AdminTenants;
using Xunit;

namespace KasseAPI_Final.Tests;

public class AdminTenantCsvExportServiceTests
{
    [Fact]
    public void BuildCsv_includes_header_and_row_values()
    {
        var sut = new AdminTenantCsvExportService();
        var rows = new List<AdminTenantListItemDto>
        {
            new(
                Id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Name: "Cafe Test",
                Slug: "cafe-test",
                Email: "a@example.com",
                Phone: null,
                Status: "active",
                IsActive: true,
                LicenseKey: "REGK-1",
                LicenseValidUntilUtc: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt: new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                UpdatedAt: null,
                LicenseDaysRemaining: 100,
                OwnerAdminEmail: "admin@cafe-test.regkasse.at",
                IsDemoPreset: false,
                LicenseType: LicenseType.Business,
                RegisterCount: 2,
                UserCount: 5,
                LastActivityAtUtc: new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc)),
        };

        var bytes = sut.BuildCsv(rows);
        var text = Encoding.UTF8.GetString(bytes);

        Assert.Contains("Name", text);
        Assert.Contains("Slug", text);
        Assert.Contains("LicenseType", text);
        Assert.Contains("Cafe Test", text);
        Assert.Contains("cafe-test", text);
        Assert.Contains("Business", text);
        Assert.Contains("2", text);
        Assert.Contains("5", text);
    }

    [Fact]
    public void BuildCsv_empty_list_still_writes_header()
    {
        var sut = new AdminTenantCsvExportService();
        var bytes = sut.BuildCsv([]);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("Name", text);
        Assert.Contains("CreatedAt", text);
        Assert.DoesNotContain("Cafe", text);
    }
}
