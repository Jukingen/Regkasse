using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ValidationRestoreExecutionServiceTests
{
    [Fact]
    public async Task ExecuteValidationRestoreAsync_rejects_when_validation_only_false()
    {
        var svc = CreateSut(Guid.NewGuid().ToString());
        var result = await svc.ExecuteValidationRestoreAsync(new ValidationRestoreExecutionRequest
        {
            BackupRunId = Guid.NewGuid(),
            ValidationOnly = false
        });

        Assert.False(result.Success);
        Assert.Contains("ValidationOnly", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteValidationRestoreAsync_fails_when_backup_missing()
    {
        var svc = CreateSut(Guid.NewGuid().ToString());
        var result = await svc.ExecuteValidationRestoreAsync(new ValidationRestoreExecutionRequest
        {
            BackupRunId = Guid.NewGuid(),
            TargetDatabaseName = "restore_validation_test",
            ValidationOnly = true
        });

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    private static ValidationRestoreExecutionService CreateSut(string dbName)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options);

        var config = new ConfigurationBuilder().Build();
        var hostEnv = new Mock<IHostEnvironment>();
        hostEnv.Setup(h => h.IsProduction()).Returns(false);
        hostEnv.Setup(h => h.ContentRootPath).Returns(Directory.GetCurrentDirectory());

        var backupOpts = Options.Create(new BackupOptions());
        var restoreOpts = Options.Create(new RestoreVerificationOptions
        {
            IsolatedPgRestoreEnabled = false
        });

        var guard = new ManualRestoreTargetDatabaseGuard(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=localhost;Database=app;Username=u;Password=p"
                })
                .Build(),
            new OptionsMonitorStub<ManualRestoreApprovalOptions>(new ManualRestoreApprovalOptions()));

        return new ValidationRestoreExecutionService(
            db,
            config,
            hostEnv.Object,
            new OptionsMonitorStub<BackupOptions>(backupOpts.Value),
            new OptionsMonitorStub<RestoreVerificationOptions>(restoreOpts.Value),
            Mock.Of<IPgRestoreIsolatedRestoreRunner>(),
            Mock.Of<IPostRestoreDrillSqlChecker>(),
            Mock.Of<IFiscalGoLiveValidationRunner>(),
            guard,
            NullLogger<ValidationRestoreExecutionService>.Instance);
    }

    private sealed class OptionsMonitorStub<T> : IOptionsMonitor<T> where T : class, new()
    {
        public OptionsMonitorStub(T value) => CurrentValue = value;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
