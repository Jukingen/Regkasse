using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupDrillReportHostedServiceTests
{
    private static readonly DateTime MondayUtc = new(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Disabled_DoesNotSendOrAudit()
    {
        var email = new Mock<IEmailService>(MockBehavior.Strict);
        var audit = new Mock<IAuditLogService>(MockBehavior.Strict);
        var sut = CreateSut(email.Object, audit.Object, new BackupDrillReportOptions
        {
            Enabled = false,
            DayOfWeekUtc = 1,
            Recipients = "ops@example.com",
        });

        await sut.RunCycleAsync(CancellationToken.None);

        email.VerifyNoOtherCalls();
        audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnabledWithEmptyRecipients_LogsAndAuditsSkipWithoutSending()
    {
        Assert.Equal(145, (int)AuditEventType.BackupDrillReportSkipped);
        var email = new Mock<IEmailService>(MockBehavior.Strict);
        var audit = AuditMock();
        var logger = new ListLogger();
        var sut = CreateSut(
            email.Object,
            audit.Object,
            new BackupDrillReportOptions { Enabled = true, DayOfWeekUtc = 1, Recipients = "  " },
            logger);

        await sut.RunCycleAsync(CancellationToken.None);

        Assert.Contains(logger.Information, line => line.Contains("no recipients", StringComparison.Ordinal));
        email.VerifyNoOtherCalls();
        audit.Verify(a => a.LogSystemOperationAsync(
                "BACKUP_DRILL_REPORT_SKIPPED",
                "BackupDrillReport",
                "system",
                "System",
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.BackupDrillReportSkipped,
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.Is<object?>(values =>
                    values != null
                    && JsonSerializer.Serialize(values).Contains("no-recipients", StringComparison.Ordinal)),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task EnabledWithRecipients_SendsAndAudits()
    {
        Assert.Equal(143, (int)AuditEventType.BackupDrillReportSent);
        Assert.Equal(144, (int)AuditEventType.BackupDrillReportFailed);

        var email = new Mock<IEmailService>();
        string? plain = null;
        email.Setup(m => m.TrySendHtmlAndPlainAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<string>, string, string, string, CancellationToken>((_, _, _, body, _) => plain = body)
            .ReturnsAsync(true);

        var audit = AuditMock();
        var proof = new DateTime(2026, 10, 12, 6, 0, 0, DateTimeKind.Utc);
        var sut = CreateSut(
            email.Object,
            audit.Object,
            new BackupDrillReportOptions
            {
                Enabled = true,
                DayOfWeekUtc = 1,
                Recipients = "ops@example.com",
            },
            summary: new BackupRecoverabilitySummaryResponseDto
            {
                LastSuccessfulRestoreProofAt = proof,
                RestoreProofAgeSeconds = 3 * 86400,
            });

        await sut.RunCycleAsync(CancellationToken.None);

        Assert.NotNull(plain);
        Assert.Contains("lastSuccessfulRestoreProofAt: 2026-10-12T06:00:00.0000000Z", plain, StringComparison.Ordinal);
        Assert.Contains("lastSuccessfulRestoreDrillAtUtc: 2026-10-12T06:00:00.0000000Z", plain, StringComparison.Ordinal);
        Assert.Contains("daysSinceLastProof: 3", plain, StringComparison.Ordinal);
        Assert.Contains("nextScheduledDrillUtc:", plain, StringComparison.Ordinal);
        Assert.Contains("drillSuccessRateLast30Days:", plain, StringComparison.Ordinal);
        Assert.Contains("knownGapAcceptances:", plain, StringComparison.Ordinal);
        email.Verify(m => m.TrySendHtmlAndPlainAsync(
                It.Is<IReadOnlyCollection<string>>(list => list.Contains("ops@example.com")),
                "[Regkasse] Weekly backup drill summary",
                It.Is<string>(html => html.Contains("lastSuccessfulRestoreProofAt", StringComparison.Ordinal)),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        audit.Verify(a => a.LogSystemOperationAsync(
                "BACKUP_DRILL_REPORT_SENT",
                "BackupDrillReport",
                "system",
                "System",
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.BackupDrillReportSent,
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task EnabledWithRecipients_WhenSendReturnsFalse_AuditsFailure()
    {
        var email = new Mock<IEmailService>();
        email.Setup(m => m.TrySendHtmlAndPlainAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var audit = AuditMock();
        var sut = CreateSut(
            email.Object,
            audit.Object,
            new BackupDrillReportOptions { Enabled = true, DayOfWeekUtc = 1, Recipients = "ops@example.com" });

        await sut.RunCycleAsync(CancellationToken.None);

        audit.Verify(a => a.LogSystemOperationAsync(
                "BACKUP_DRILL_REPORT_FAILED",
                "BackupDrillReport",
                "system",
                "System",
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.BackupDrillReportFailed,
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    private static BackupDrillReportHostedService CreateSut(
        IEmailService email,
        IAuditLogService audit,
        BackupDrillReportOptions options,
        ILogger<BackupDrillReportHostedService>? logger = null,
        BackupRecoverabilitySummaryResponseDto? summary = null)
    {
        var summaryMock = new Mock<IBackupRecoverabilitySummaryService>();
        summaryMock
            .Setup(s => s.GetAsync(It.IsAny<BackupRunAccessScope?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary ?? new BackupRecoverabilitySummaryResponseDto());

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"drill-report_{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(dbOptions, TenantTestDoubles.TenantAccessorReturning(null));

        var services = new ServiceCollection();
        services.AddSingleton(summaryMock.Object);
        services.AddSingleton(email);
        services.AddSingleton(audit);
        services.AddSingleton(db);
        var provider = services.BuildServiceProvider();

        var reportOptions = new Mock<IOptionsMonitor<BackupDrillReportOptions>>();
        reportOptions.Setup(m => m.CurrentValue).Returns(options);
        var restoreOptions = new Mock<IOptionsMonitor<RestoreVerificationOptions>>();
        restoreOptions.Setup(m => m.CurrentValue).Returns(new RestoreVerificationOptions());

        return new BackupDrillReportHostedService(
            reportOptions.Object,
            restoreOptions.Object,
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedClock(MondayUtc),
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BackupDrillReportHostedService>.Instance);
    }

    private static Mock<IAuditLogService> AuditMock()
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
        return audit;
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private sealed class ListLogger : ILogger<BackupDrillReportHostedService>
    {
        public List<string> Information { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Information)
                Information.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
