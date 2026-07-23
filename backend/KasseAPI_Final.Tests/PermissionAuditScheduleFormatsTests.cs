using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PermissionAuditScheduleFormatsTests
{
    [Theory]
    [InlineData("permission-csv", true)]
    [InlineData("permission-json", true)]
    [InlineData("permission-pdf", true)]
    [InlineData("csv", false)]
    [InlineData("json", false)]
    [InlineData(null, false)]
    public void IsPermissionFormat_detects_schedule_formats(string? format, bool expected)
    {
        Assert.Equal(expected, PermissionAuditScheduleFormats.IsPermissionFormat(format));
    }
}
