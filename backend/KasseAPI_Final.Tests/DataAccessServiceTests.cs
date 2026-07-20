using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DataAccess;
using KasseAPI_Final.Services.DataRights;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DataAccessServiceTests
{
    [Fact]
    public async Task ProcessRequest_View_AutoApprovesAndSucceeds()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var rightsDto = new TenantDataRightsRequestDto
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RequestType = TenantDataRightsRequestTypes.View,
            Status = TenantDataRightsRequestStatuses.Completed,
            ApprovalMode = TenantDataRightsApprovalModes.Auto,
            RequestedByUserId = userId.ToString("D"),
            RequestedAtUtc = DateTime.UtcNow,
            ApprovedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow,
        };

        var rights = new Mock<ICustomerDataRightsService>();
        rights.Setup(r => r.CreateAsync(
                tenantId,
                TenantDataRightsRequestTypes.View,
                null,
                userId.ToString("D"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(rightsDto);

        var notify = new Mock<IDataAccessNotificationService>();
        var sut = new DataAccessService(rights.Object, notify.Object, NullLogger<DataAccessService>.Instance);

        var result = await sut.ProcessRequestAsync(tenantId, DataRequestType.View, userId);

        Assert.True(result.Succeeded);
        Assert.False(result.IsPending);
        Assert.NotNull(result.Request);
        Assert.Equal(DataRequestType.View, result.Request!.Type);
        Assert.Equal(TenantDataRightsRequestStatuses.Approved, result.Request.Status);
        notify.Verify(
            n => n.NotifySuperAdminAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessRequest_Export_AutoApprovesWithoutSuperAdminNotify()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var rightsDto = new TenantDataRightsRequestDto
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RequestType = TenantDataRightsRequestTypes.Export,
            Status = TenantDataRightsRequestStatuses.Ready,
            ApprovalMode = TenantDataRightsApprovalModes.Auto,
            RequestedByUserId = userId.ToString("D"),
            RequestedAtUtc = DateTime.UtcNow,
            ApprovedAtUtc = DateTime.UtcNow,
            CanDownload = true,
        };

        var rights = new Mock<ICustomerDataRightsService>();
        rights.Setup(r => r.CreateAsync(
                tenantId,
                TenantDataRightsRequestTypes.Export,
                null,
                userId.ToString("D"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(rightsDto);

        var notify = new Mock<IDataAccessNotificationService>();
        var sut = new DataAccessService(rights.Object, notify.Object, NullLogger<DataAccessService>.Instance);

        var result = await sut.ProcessRequestAsync(tenantId, DataRequestType.Export, userId);

        Assert.True(result.Succeeded);
        Assert.False(result.IsPending);
        notify.Verify(
            n => n.NotifySuperAdminAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessRequest_Delete_PendingApproval_NotifiesSuperAdmin()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var rightsDto = new TenantDataRightsRequestDto
        {
            Id = requestId,
            TenantId = tenantId,
            RequestType = TenantDataRightsRequestTypes.Delete,
            Status = TenantDataRightsRequestStatuses.PendingApproval,
            ApprovalMode = TenantDataRightsApprovalModes.Manual,
            RequestedByUserId = userId.ToString("D"),
            RequestedAtUtc = DateTime.UtcNow,
            CanConfirm = true,
        };

        var rights = new Mock<ICustomerDataRightsService>();
        rights.Setup(r => r.CreateAsync(
                tenantId,
                TenantDataRightsRequestTypes.Delete,
                "cleanup",
                userId.ToString("D"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(rightsDto);

        var notify = new Mock<IDataAccessNotificationService>();
        notify.Setup(n => n.NotifySuperAdminAsync(
                tenantId,
                requestId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new DataAccessService(rights.Object, notify.Object, NullLogger<DataAccessService>.Instance);

        var result = await sut.ProcessRequestAsync(
            tenantId,
            DataRequestType.Delete,
            userId,
            reason: "cleanup");

        Assert.True(result.Succeeded);
        Assert.True(result.IsPending);
        Assert.Equal(TenantDataRightsRequestStatuses.PendingApproval, result.Request!.Status);
        notify.Verify(
            n => n.NotifySuperAdminAsync(
                tenantId,
                requestId,
                "Data deletion request",
                It.Is<string>(b => b.Contains(tenantId.ToString(), StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("view", DataRequestType.View)]
    [InlineData("EXPORT", DataRequestType.Export)]
    [InlineData("delete", DataRequestType.Delete)]
    public void TryParse_KnownTypes(string raw, DataRequestType expected)
    {
        Assert.True(DataRequestTypeExtensions.TryParse(raw, out var type));
        Assert.Equal(expected, type);
    }
}
