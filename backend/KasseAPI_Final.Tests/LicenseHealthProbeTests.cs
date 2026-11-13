using KasseAPI_Final.Data;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseHealthProbeTests
{
    [Fact]
    public async Task CheckAsync_InMemoryDb_IsHealthyWhenSnapshotInitialized()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LicenseHealth_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var db = new AppDbContext(options, NullCurrentTenantAccessor.Instance);

        var license = new Mock<ILicenseService>();
        license.Setup(x => x.IsLicenseSnapshotInitialized).Returns(true);
        license.Setup(x => x.GetStatus()).Returns(new LicenseStatusResponse(
            true,
            false,
            false,
            10,
            DateTime.UtcNow.AddDays(10),
            "abc"));

        var reminders = new Mock<ILicenseReminderNotificationStore>();
        reminders.Setup(x => x.GetReminders()).Returns([]);

        var result = await LicenseHealthProbe.CheckAsync(license.Object, reminders.Object, db);

        Assert.Equal(LicenseHealthProbe.Healthy, result.Status);
        Assert.True(result.DatabaseConnected);
        Assert.True(result.IssuedLicensesTableOk);
        Assert.True(result.LicenseSalesTableOk);
        Assert.True(result.SampleQueryOk);
        Assert.True(result.IsValid);
    }
}
