using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupReVerificationHostedServiceTests
{
    [Fact]
    public async Task Execute_WhenDisabled_NoOp()
    {
        var alerts = new Mock<IBackupAlertPublisher>(MockBehavior.Strict);
        var services = new ServiceCollection();
        services.AddSingleton(CreateDb());
        services.AddScoped<IBackupChecksumVerificationService>(_ =>
            Mock.Of<IBackupChecksumVerificationService>());
        await using var provider = services.BuildServiceProvider();

        var sut = CreateSut(provider, new BackupReVerificationOptions { Enabled = false }, alerts.Object);
        await sut.RunTickAsync(CancellationToken.None);
        alerts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Execute_VerifiesRecentRuns_And_FailurePublishesAlert()
    {
        await using var db = CreateDb();
        var runId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow.AddHours(-2),
            CompletedAt = DateTime.UtcNow.AddHours(-1),
        });
        await db.SaveChangesAsync();

        var verifier = new Mock<IBackupChecksumVerificationService>();
        verifier
            .Setup(v => v.VerifyAndPersistAsync(
                runId,
                IBackupChecksumVerificationService.VerifierSourceScheduledReverify,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KasseAPI_Final.DTOs.BackupChecksumVerifyResponseDto
            {
                RunId = runId,
                IsValid = false,
                VerifiedAtUtc = DateTime.UtcNow,
                VerifierSource = IBackupChecksumVerificationService.VerifierSourceScheduledReverify,
                FailureReason = "mismatch",
            });

        var alerts = new Mock<IBackupAlertPublisher>();
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddScoped(_ => verifier.Object);
        await using var provider = services.BuildServiceProvider();

        var sut = CreateSut(
            provider,
            new BackupReVerificationOptions
            {
                Enabled = true,
                RetentionDays = 7,
                MaxRunsPerTick = 10,
                CheckIntervalHours = 24,
            },
            alerts.Object);

        await sut.RunTickAsync(CancellationToken.None);

        verifier.Verify(
            v => v.VerifyAndPersistAsync(
                runId,
                IBackupChecksumVerificationService.VerifierSourceScheduledReverify,
                It.IsAny<CancellationToken>()),
            Times.Once);
        alerts.Verify(
            a => a.Publish(It.Is<BackupAlertEvent>(e =>
                e.Kind == BackupAlertKind.VerificationFailed && e.BackupRunId == runId)),
            Times.Once);
    }

    [Fact]
    public async Task Execute_SkipsRecentlyVerifiedRuns()
    {
        await using var db = CreateDb();
        var runId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow.AddHours(-2),
            CompletedAt = DateTime.UtcNow.AddHours(-1),
        });
        db.BackupVerifications.Add(new BackupVerification
        {
            BackupRunId = runId,
            Status = BackupVerificationStatus.Passed,
            StartedAt = DateTime.UtcNow.AddMinutes(-30),
            CompletedAt = DateTime.UtcNow.AddMinutes(-30),
            VerifierSource = IBackupChecksumVerificationService.VerifierSourceScheduledReverify,
        });
        await db.SaveChangesAsync();

        var verifier = new Mock<IBackupChecksumVerificationService>(MockBehavior.Strict);
        var alerts = new Mock<IBackupAlertPublisher>(MockBehavior.Strict);
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddScoped(_ => verifier.Object);
        await using var provider = services.BuildServiceProvider();

        var sut = CreateSut(
            provider,
            new BackupReVerificationOptions
            {
                Enabled = true,
                RetentionDays = 7,
                CheckIntervalHours = 24,
            },
            alerts.Object);

        await sut.RunTickAsync(CancellationToken.None);
        verifier.VerifyNoOtherCalls();
        alerts.VerifyNoOtherCalls();
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"reverify_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private static BackupReVerificationHostedService CreateSut(
        IServiceProvider provider,
        BackupReVerificationOptions options,
        IBackupAlertPublisher alerts)
    {
        var monitor = new Mock<IOptionsMonitor<BackupReVerificationOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(options);
        return new BackupReVerificationHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            monitor.Object,
            alerts,
            NullLogger<BackupReVerificationHostedService>.Instance);
    }
}
