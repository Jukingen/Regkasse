using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.AccountClosure;
using KasseAPI_Final.Services.DataDeletion;
using KasseAPI_Final.Services.DataExport;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AccountClosureServiceTests
{
    [Fact]
    public async Task RequestClosureAsync_MapsDeletionDto()
    {
        var tenantId = Guid.NewGuid();
        var closureId = Guid.NewGuid();
        var deletion = new Mock<IDataDeletionService>();
        deletion.Setup(d => d.RequestDeletionAsync(tenantId, "user-1", "leaving", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantDataDeletionRequestDto
            {
                Id = closureId,
                Status = TenantDataDeletionRequestStatuses.Pending,
                Reason = "leaving",
                RequestedAtUtc = DateTime.UtcNow,
                ConfirmationWaitDays = 7,
            });

        var factory = CreateEmptyDbFactory();
        var sut = new AccountClosureService(deletion.Object, factory, NullLogger<AccountClosureService>.Instance);

        var result = await sut.RequestClosureAsync(tenantId, "leaving", "user-1");

        Assert.True(result.Succeeded);
        Assert.Equal(closureId, result.ClosureId);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal(TenantDataDeletionRequestStatuses.Pending, result.Status);
        Assert.Equal(7, result.ConfirmationWaitDays);
        Assert.False(result.HasRksvData);
    }

    [Fact]
    public async Task RequestClosureAsync_NotArchived_FailsWithCode()
    {
        var tenantId = Guid.NewGuid();
        var deletion = new Mock<IDataDeletionService>();
        deletion.Setup(d => d.RequestDeletionAsync(
                tenantId, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Data deletion requires an Archived license (more than 30 days overdue)."));

        var factory = CreateEmptyDbFactory();
        var sut = new AccountClosureService(deletion.Object, factory, NullLogger<AccountClosureService>.Instance);

        var result = await sut.RequestClosureAsync(tenantId, "reason");

        Assert.False(result.Succeeded);
        Assert.Equal(DataDeletionErrorCodes.NotArchived, result.ErrorCode);
    }

    [Fact]
    public async Task CancelClosureAsync_NoOpenRequest_Fails()
    {
        var tenantId = Guid.NewGuid();
        var deletion = new Mock<IDataDeletionService>();
        deletion.Setup(d => d.GetLatestOpenDeletionAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantDataDeletionRequestDto?)null);

        var factory = CreateEmptyDbFactory();
        var sut = new AccountClosureService(deletion.Object, factory, NullLogger<AccountClosureService>.Instance);

        var result = await sut.CancelClosureAsync(tenantId, "user-1");

        Assert.False(result.Succeeded);
        Assert.Equal(DataDeletionErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CancelClosureAsync_CancelsOpenRequest()
    {
        var tenantId = Guid.NewGuid();
        var closureId = Guid.NewGuid();
        var deletion = new Mock<IDataDeletionService>();
        deletion.Setup(d => d.GetLatestOpenDeletionAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantDataDeletionRequestDto
            {
                Id = closureId,
                Status = TenantDataDeletionRequestStatuses.Pending,
                RequestedAtUtc = DateTime.UtcNow,
                ConfirmationWaitDays = 7,
            });
        deletion.Setup(d => d.CancelDeletionAsync(tenantId, closureId, "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantDataDeletionRequestDto
            {
                Id = closureId,
                Status = TenantDataDeletionRequestStatuses.Cancelled,
                RequestedAtUtc = DateTime.UtcNow,
                ConfirmationWaitDays = 7,
            });

        var factory = CreateEmptyDbFactory();
        var sut = new AccountClosureService(deletion.Object, factory, NullLogger<AccountClosureService>.Instance);

        var result = await sut.CancelClosureAsync(tenantId, "user-1");

        Assert.True(result.Succeeded);
        Assert.Equal(TenantDataDeletionRequestStatuses.Cancelled, result.Status);
        Assert.Equal(closureId, result.ClosureId);
    }

    [Fact]
    public async Task ExecuteClosureAsync_PropagatesPurgeFailure()
    {
        var closureId = Guid.NewGuid();
        var deletion = new Mock<IDataDeletionService>();
        deletion.Setup(d => d.ExecutePurgeAsync(
                closureId, It.IsAny<string?>(), TenantDataDeletionExecutedVia.Manual, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeletionResult.Fail("Grace period not yet completed", DataDeletionErrorCodes.GracePeriodActive));

        var factory = CreateEmptyDbFactory();
        var sut = new AccountClosureService(deletion.Object, factory, NullLogger<AccountClosureService>.Instance);

        var result = await sut.ExecuteClosureAsync(closureId, "admin");

        Assert.False(result.Succeeded);
        Assert.Equal(DataDeletionErrorCodes.GracePeriodActive, result.ErrorCode);
    }

    [Fact]
    public void ExportResult_FailAndSuccess_Factories()
    {
        var fail = ExportResult.Fail("missing", DataExportErrorCodes.NotFound);
        Assert.False(fail.Succeeded);
        Assert.Equal(DataExportErrorCodes.NotFound, fail.ErrorCode);

        var id = Guid.NewGuid();
        var ok = ExportResult.Success(id, Guid.NewGuid(), TenantDataRightsRequestStatuses.Processing);
        Assert.True(ok.Succeeded);
        Assert.Equal(id, ok.RequestId);
        Assert.Equal(TenantDataRightsRequestStatuses.Processing, ok.Status);
    }

    private static IDbContextFactory<AppDbContext> CreateEmptyDbFactory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"closure_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AppDbContext(options, NullCurrentTenantAccessor.Instance));
        factory.Setup(f => f.CreateDbContext())
            .Returns(() => new AppDbContext(options, NullCurrentTenantAccessor.Instance));

        return factory.Object;
    }
}
