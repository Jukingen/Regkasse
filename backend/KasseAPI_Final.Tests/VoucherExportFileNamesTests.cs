using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Vouchers;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class VoucherExportFileNamesTests
{
    [Fact]
    public void Build_json_matches_canonical_pattern()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22);
        Assert.Equal("voucher_cafe_20260722_143022.json", VoucherExportFileNames.Build("cafe", "json", at));
    }

    [Fact]
    public void Build_csv_matches_canonical_pattern()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22);
        Assert.Equal("voucher_cafe_20260722_143022.csv", VoucherExportFileNames.Build("cafe", "csv", at));
    }
}

public sealed class VoucherExportRedactionTests
{
    [Theory]
    [InlineData("****1234", "****1234")]
    [InlineData("ABCD1234EFGH", "****EFGH")]
    [InlineData("12", "****12")]
    [InlineData("", "***")]
    [InlineData(null, "***")]
    public void RedactCodeHint_never_exposes_full_code(string? input, string expected)
    {
        Assert.Equal(expected, VoucherExportService.RedactCodeHint(input));
    }
}
