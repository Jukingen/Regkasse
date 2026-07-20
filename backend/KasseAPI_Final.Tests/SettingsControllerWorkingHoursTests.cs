using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class SettingsControllerWorkingHoursTests
{
    private static readonly Guid TenantId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    private static (AppDbContext Db, SettingsController Controller) Create(Guid? tenantId)
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"WorkingHours_{Guid.NewGuid()}")
            .Options;
        var db = new AppDbContext(options, tenantAccessor);
        var settingsTenantResolver = new Mock<ISettingsTenantResolver>();
        settingsTenantResolver
            .Setup(r => r.ResolveEffectiveTenantIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantId ?? Guid.Empty);
        var controller = new SettingsController(
            db,
            NullLogger<SettingsController>.Instance,
            Mock.Of<IBackupManualTriggerService>(),
            settingsTenantResolver.Object,
            Mock.Of<ICashRegisterSettingsService>(),
            tenantAccessor);
        return (db, controller);
    }

    [Fact]
    public async Task GetWorkingHours_WithoutTenant_Returns404()
    {
        var (db, controller) = Create(null);
        await using (db)
        {
            var result = await controller.GetWorkingHours(CancellationToken.None);
            Assert.IsType<NotFoundResult>(result);
        }
    }

    [Fact]
    public async Task GetWorkingHours_WhenNoRow_ReturnsDefaults()
    {
        var (db, controller) = Create(TenantId);
        await using (db)
        {
            var result = await controller.GetWorkingHours(CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<WorkingHoursDto>(ok.Value);
            Assert.Equal(1, dto.ReminderHoursBeforeClosing);
            Assert.Equal("09:00", dto.Monday.OpenTime);
            Assert.Equal("22:00", dto.Monday.CloseTime);
        }
    }

    [Fact]
    public async Task UpdateWorkingHours_PersistsAndReturnsNormalized()
    {
        var (db, controller) = Create(TenantId);
        await using (db)
        {
            var put = await controller.UpdateWorkingHours(
                new WorkingHoursDto
                {
                    ReminderHoursBeforeClosing = 2,
                    Saturday = new WorkingHoursDay
                    {
                        OpenTime = "11:00",
                        CloseTime = "01:00",
                        IsClosed = false,
                    },
                    Sunday = new WorkingHoursDay { IsClosed = true },
                },
                CancellationToken.None);

            var putOk = Assert.IsType<OkObjectResult>(put);
            var putDto = Assert.IsType<WorkingHoursDto>(putOk.Value);
            Assert.Equal(2, putDto.ReminderHoursBeforeClosing);
            Assert.Equal("11:00", putDto.Saturday.OpenTime);
            Assert.Equal("01:00", putDto.Saturday.CloseTime);
            Assert.True(putDto.Sunday.IsClosed);

            var get = await controller.GetWorkingHours(CancellationToken.None);
            var getOk = Assert.IsType<OkObjectResult>(get);
            var getDto = Assert.IsType<WorkingHoursDto>(getOk.Value);
            Assert.Equal(2, getDto.ReminderHoursBeforeClosing);
            Assert.Equal("11:00", getDto.Saturday.OpenTime);
            Assert.True(getDto.Sunday.IsClosed);
        }
    }
}
