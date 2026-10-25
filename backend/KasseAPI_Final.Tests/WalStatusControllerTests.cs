using System.Reflection;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class WalStatusControllerTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetWalLiveStatus_Healthy_WhenLagUnder60AndNoFailures()
    {
        var dto = Read(Observation(
            configured: true,
            successAt: Now.AddSeconds(-30),
            segmentName: "0000000100000000000000AB",
            archived: 4,
            failed: 0));

        Assert.Equal(WalArchiveHealthLevel.Healthy, dto.HealthLevel);
        Assert.Equal(WalStatusEvaluator.HealthyReason, dto.HealthReason);
        Assert.Equal(30, dto.LagSeconds);
        Assert.Equal(0, dto.FailedCountLast24h);
        Assert.Equal("0000000100000000000000AB", dto.LastSuccessfulSegmentName);
    }

    [Fact]
    public void GetWalLiveStatus_Degraded_WhenLagIs120Seconds()
    {
        var dto = Read(Observation(
            configured: true,
            successAt: Now.AddSeconds(-120),
            segmentName: "0000000100000000000000AC",
            archived: 2,
            failed: 0));

        Assert.Equal(WalArchiveHealthLevel.Degraded, dto.HealthLevel);
        Assert.Equal(WalStatusEvaluator.LagDegradedReason, dto.HealthReason);
        Assert.Equal(120, dto.LagSeconds);
    }

    [Fact]
    public void GetWalLiveStatus_Critical_WhenLagIs600Seconds()
    {
        var dto = Read(Observation(
            configured: true,
            successAt: Now.AddSeconds(-600),
            segmentName: "0000000100000000000000AD",
            archived: 1,
            failed: 0));

        Assert.Equal(WalArchiveHealthLevel.Critical, dto.HealthLevel);
        Assert.Equal(WalStatusEvaluator.LagCriticalReason, dto.HealthReason);
        Assert.Equal(600, dto.LagSeconds);
    }

    [Fact]
    public void GetWalLiveStatus_Critical_WhenNoSuccessfulSegment()
    {
        var dto = Read(Observation(
            configured: true,
            successAt: null,
            segmentName: null,
            archived: 0,
            failed: 0));

        Assert.Equal(WalArchiveHealthLevel.Critical, dto.HealthLevel);
        Assert.Equal(WalStatusEvaluator.NoSegmentReason, dto.HealthReason);
        Assert.Null(dto.LagSeconds);
        Assert.Null(dto.LastSuccessfulSegmentAtUtc);
        Assert.Null(dto.LastSuccessfulSegmentName);
    }

    [Fact]
    public void GetWalLiveStatus_Critical_WhenFailedCountLast24hIsAtLeast3()
    {
        var dto = Read(Observation(
            configured: true,
            successAt: Now.AddSeconds(-10),
            segmentName: "0000000100000000000000AE",
            archived: 8,
            failed: 3,
            failureReason: WalArchiveService.InventoryUnreadableReason));

        Assert.Equal(WalArchiveHealthLevel.Critical, dto.HealthLevel);
        Assert.Equal(WalStatusEvaluator.FailuresReason, dto.HealthReason);
        Assert.Equal(3, dto.FailedCountLast24h);
        Assert.Equal(10, dto.LagSeconds);
        Assert.Equal(WalArchiveService.InventoryUnreadableReason, dto.LastFailureReason);
    }

    [Fact]
    public void GetWalLiveStatus_Critical_WhenWalArchivingDisabledByConfig()
    {
        var dto = Read(Observation(
            configured: false,
            successAt: Now.AddSeconds(-15),
            segmentName: "0000000100000000000000AF",
            archived: 1,
            failed: 0));

        Assert.Equal(WalArchiveHealthLevel.Critical, dto.HealthLevel);
        Assert.Equal("wal-archive-disabled", dto.HealthReason);
    }

    [Fact]
    public void GetWalLiveStatus_ResponseOmitsWalBytesPathsAndSecrets()
    {
        var plantedPath = @"C:\ProgramData\secrets\wal-archive\000000010000000000000001";
        var plantedSecret = "password=super-secret-token";
        var dto = Read(new WalArchiveObservation
        {
            Configured = true,
            LastSuccessfulSegmentAtUtc = Now.AddSeconds(-20),
            LastSuccessfulSegmentName = plantedPath,
            LastAttemptedSegmentAtUtc = Now.AddSeconds(-20),
            ArchivedCountLast24h = 1,
            FailedCountLast24h = 0,
            LastFailureReason = plantedSecret,
        });

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.Equal("000000010000000000000001", dto.LastSuccessfulSegmentName);
        Assert.Null(dto.LastFailureReason);
        Assert.DoesNotContain("ProgramData", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("super-secret-token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\", json, StringComparison.Ordinal);
        Assert.DoesNotContain("/", json, StringComparison.Ordinal);
        Assert.DoesNotContain("directory", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("byteSize", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WALBYTES", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GetWalLiveStatus_RequiresSystemCriticalOnWalStatusRoute()
    {
        var method = typeof(AdminBackupPitrController).GetMethod(nameof(AdminBackupPitrController.GetWalLiveStatus));
        Assert.NotNull(method);

        var route = method!.GetCustomAttribute<HttpGetAttribute>();
        Assert.Equal("pitr/wal-status", route?.Template);

        var permission = method.GetCustomAttribute<HasPermissionAttribute>();
        Assert.Equal(AppPermissions.SystemCritical, permission?.Permission);

        var role = method.GetCustomAttributes<AuthorizeAttribute>(inherit: false)
            .Single(attribute => attribute is not HasPermissionAttribute);
        Assert.Equal(Roles.SuperAdmin, role.Roles);
    }

    private static WalArchiveObservation Observation(
        bool configured,
        DateTime? successAt,
        string? segmentName,
        int archived,
        int failed,
        string? failureReason = null) =>
        new()
        {
            Configured = configured,
            LastSuccessfulSegmentAtUtc = successAt,
            LastSuccessfulSegmentName = segmentName,
            LastAttemptedSegmentAtUtc = successAt,
            ArchivedCountLast24h = archived,
            FailedCountLast24h = failed,
            LastFailureReason = failureReason,
        };

    private static WalStatusDto Read(WalArchiveObservation observation)
    {
        var wal = new Mock<IWalArchiveService>();
        wal.Setup(w => w.GetObservation()).Returns(observation);
        var controller = new AdminBackupPitrController(
            Mock.Of<IPitrService>(),
            wal.Object,
            Mock.Of<IBackupChainService>(),
            Mock.Of<IIncrementalBackupService>(),
            Mock.Of<ICurrentTenantAccessor>(),
            new FixedUtcTimeProvider(Now));

        var result = controller.GetWalLiveStatus();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<WalStatusDto>(ok.Value);
    }

    private sealed class FixedUtcTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedUtcTimeProvider(DateTime utc) =>
            _utcNow = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
