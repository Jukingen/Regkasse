using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupFailureEmailAlertServiceTests
{
    [Fact]
    public void BuildSubject_uses_german_ops_wording_with_tenant_slug()
    {
        var subject = BackupFailureEmailAlertService.BuildSubject("demo");
        Assert.Equal("⚠️ Backup fehlgeschlagen: demo", subject);
    }

    [Fact]
    public void BuildBody_includes_mandant_error_and_utc_time()
    {
        var utc = new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);
        var runId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var body = BackupFailureEmailAlertService.BuildBody(
            "acme",
            "pg_dump exited with code 1",
            runId,
            "corr-1",
            utc);

        Assert.Contains("Backup für Mandant acme ist fehlgeschlagen.", body, StringComparison.Ordinal);
        Assert.Contains("Fehler: pg_dump exited with code 1", body, StringComparison.Ordinal);
        Assert.Contains("Zeit (UTC):", body, StringComparison.Ordinal);
        Assert.Contains(runId.ToString("D"), body, StringComparison.Ordinal);
        Assert.Contains("Korrelations-ID: corr-1", body, StringComparison.Ordinal);
        Assert.Contains("Bitte überprüfen Sie das System.", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeError_truncates_long_messages()
    {
        var longError = new string('x', BackupFailureEmailAlertService.MaxErrorLength + 50);
        var sanitized = BackupFailureEmailAlertService.SanitizeError(longError);
        Assert.True(sanitized.Length <= BackupFailureEmailAlertService.MaxErrorLength + 1);
        Assert.EndsWith("…", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeSlug_falls_back_to_deployment_when_empty()
    {
        Assert.Equal(
            BackupRunTenantSlugResolver.DeploymentSlug,
            BackupFailureEmailAlertService.SanitizeSlug("  "));
    }
}

public sealed class EmailBackupAlertPublisherTests
{
    [Fact]
    public async Task Publish_BackupFailed_invokes_email_with_tenant_slug_from_data()
    {
        var email = new Mock<IBackupFailureEmailAlertService>();
        email
            .Setup(e => e.SendFailureAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection().BuildServiceProvider();
        var publisher = new EmailBackupAlertPublisher(
            services.GetRequiredService<IServiceScopeFactory>(),
            email.Object,
            NullLogger<EmailBackupAlertPublisher>.Instance);

        var runId = Guid.NewGuid();
        publisher.Publish(new BackupAlertEvent(
            BackupAlertKind.BackupFailed,
            runId,
            "c1",
            "disk full",
            new Dictionary<string, string>
            {
                ["tenantSlug"] = "demo",
                ["errorCode"] = "EXECUTION_FAILED",
            }));

        await WaitForAsync(() => email.Invocations.Count > 0);

        email.Verify(
            e => e.SendFailureAlertAsync(
                "demo",
                "EXECUTION_FAILED: disk full",
                runId,
                "c1",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Publish_StoragePressure_does_not_send_failure_email()
    {
        var email = new Mock<IBackupFailureEmailAlertService>();
        var services = new ServiceCollection().BuildServiceProvider();
        var publisher = new EmailBackupAlertPublisher(
            services.GetRequiredService<IServiceScopeFactory>(),
            email.Object,
            NullLogger<EmailBackupAlertPublisher>.Instance);

        publisher.Publish(new BackupAlertEvent(
            BackupAlertKind.StoragePressure,
            null,
            null,
            "budget"));

        await Task.Delay(50);

        email.Verify(
            e => e.SendFailureAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var start = Environment.TickCount64;
        while (Environment.TickCount64 - start < timeoutMs)
        {
            if (condition())
                return;
            await Task.Delay(20);
        }

        Assert.Fail("Condition was not met within timeout.");
    }
}
