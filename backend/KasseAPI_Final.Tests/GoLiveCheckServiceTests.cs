using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.Deployment;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class GoLiveCheckServiceTests
{
    [Fact]
    public async Task CheckAllConditionsAsync_is_no_go_on_unsafe_defaults()
    {
        var sut = CreateService(new Fixture());
        var status = await sut.CheckAllConditionsAsync();

        Assert.Equal(GoLiveStatusDto.StatusNoGo, status.Status);
        Assert.Equal(6, status.Checks.Count);
        Assert.Contains(status.Checks, c => !c.Passed);
        Assert.StartsWith("Missing ", status.Summary, StringComparison.Ordinal);
        Assert.All(status.Checks.Where(c => !c.Passed), c => Assert.False(string.IsNullOrWhiteSpace(c.Remediation)));
    }

    [Fact]
    public async Task CheckAllConditionsAsync_is_go_when_all_gates_pass()
    {
        var fixture = CreatePassingFixture();
        await SeedPassingEvidenceAsync(fixture.DbName);
        var sut = CreateService(fixture);

        var status = await sut.CheckAllConditionsAsync();

        Assert.Equal(GoLiveStatusDto.StatusGo, status.Status);
        Assert.Equal("All conditions met. Ready for production.", status.Summary);
        Assert.All(status.Checks, c => Assert.True(c.Passed, c.Name + ": " + c.Details));
        Assert.All(status.Checks, c => Assert.Equal(string.Empty, c.Remediation));
    }

    [Fact]
    public async Task GetLatestStatusAsync_returns_stored_result_without_rerunning_when_present()
    {
        var fixture = CreatePassingFixture();
        await SeedPassingEvidenceAsync(fixture.DbName);
        var store = new GoLiveStatusStore();
        var sut = CreateService(fixture, store);

        var first = await sut.CheckAllConditionsAsync();
        fixture.GoLive.Section8Signed = false;
        var latest = await sut.GetLatestStatusAsync();

        Assert.Equal(first.Status, latest.Status);
        Assert.Equal(GoLiveStatusDto.StatusGo, latest.Status);
        Assert.Equal(first.CheckedAtUtc, latest.CheckedAtUtc);
    }

    [Fact]
    public async Task Fiskaly_fails_when_environment_is_test()
    {
        var fixture = CreatePassingFixture();
        fixture.Fiskaly.Environment = FiskalyOptions.TestEnvironment;
        await SeedPassingEvidenceAsync(fixture.DbName);
        var sut = CreateService(fixture);

        var status = await sut.CheckAllConditionsAsync();
        var check = Assert.Single(status.Checks, c => c.Name == GoLiveCheckDto.NameFiskaly);
        Assert.False(check.Passed);
        Assert.Contains("LIVE", check.Details, StringComparison.Ordinal);
        Assert.Equal(GoLiveStatusDto.StatusNoGo, status.Status);
    }

    [Fact]
    public async Task Fon_fails_when_simulation_is_on()
    {
        var fixture = CreatePassingFixture();
        fixture.ConfigPairs = fixture.ConfigPairs
            .Where(p => p.Key != "FinanzOnline:Session:UseSimulation")
            .Append(("FinanzOnline:Session:UseSimulation", "true"))
            .ToArray();
        await SeedPassingEvidenceAsync(fixture.DbName);
        var sut = CreateService(fixture);

        var status = await sut.CheckAllConditionsAsync();
        var check = Assert.Single(status.Checks, c => c.Name == GoLiveCheckDto.NameFon);
        Assert.False(check.Passed);
        Assert.Equal(GoLiveStatusDto.StatusNoGo, status.Status);
    }

    [Fact]
    public async Task Backup_fails_without_recent_system_dump()
    {
        var fixture = CreatePassingFixture();
        await SeedPassingEvidenceAsync(fixture.DbName, includeSystemBackup: false);
        var sut = CreateService(fixture);

        var status = await sut.CheckAllConditionsAsync();
        var check = Assert.Single(status.Checks, c => c.Name == GoLiveCheckDto.NameBackup);
        Assert.False(check.Passed);
        Assert.Contains("System backup", check.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SignOff_fails_when_section8_not_attested()
    {
        var fixture = CreatePassingFixture();
        fixture.GoLive.Section8Signed = false;
        await SeedPassingEvidenceAsync(fixture.DbName);
        var sut = CreateService(fixture);

        var status = await sut.CheckAllConditionsAsync();
        var check = Assert.Single(status.Checks, c => c.Name == GoLiveCheckDto.NameSignOff);
        Assert.False(check.Passed);
        Assert.Contains("Section8Signed", check.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Configuration_fails_when_redis_disabled()
    {
        var fixture = CreatePassingFixture();
        fixture.ConfigPairs = fixture.ConfigPairs
            .Where(p => p.Key != "Redis:Enabled")
            .Append(("Redis:Enabled", "false"))
            .ToArray();
        await SeedPassingEvidenceAsync(fixture.DbName);
        var sut = CreateService(fixture);

        var status = await sut.CheckAllConditionsAsync();
        var check = Assert.Single(status.Checks, c => c.Name == GoLiveCheckDto.NameConfiguration);
        Assert.False(check.Passed);
        Assert.Contains(ProductionRuntimeConfigurationGuard.RedisMustBeEnabled, check.Details);
    }

    [Fact]
    public async Task Monitoring_fails_when_alertmanager_not_attested()
    {
        var fixture = CreatePassingFixture();
        fixture.GoLive.AlertmanagerReceiversConfigured = false;
        await SeedPassingEvidenceAsync(fixture.DbName);
        var sut = CreateService(fixture);

        var status = await sut.CheckAllConditionsAsync();
        var check = Assert.Single(status.Checks, c => c.Name == GoLiveCheckDto.NameMonitoring);
        Assert.False(check.Passed);
        Assert.Contains("Alertmanager", check.Details, StringComparison.Ordinal);
    }

    private static Fixture CreatePassingFixture()
    {
        var fixture = new Fixture
        {
            ConfigPairs = SafeProductionPairs(),
            Tse =
            {
                TseMode = "Device",
                Mode = "Real",
                Provider = TseOptions.ProviderFiskaly,
                AllowSimulatedDailyClosing = false,
                FallbackEnabled = false,
                SoftTseEnabled = false,
                AllowUnsafeFiscalModesInProduction = false,
            },
            Fiskaly =
            {
                Enabled = true,
                ApiKey = "test-key",
                ApiSecret = "test-secret",
                SignatureCreationUnitId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                Environment = FiskalyOptions.LiveEnvironment,
            },
            Backup = { ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump },
            Monitoring =
            {
                Enabled = true,
                Prometheus = { Enabled = true },
            },
            FonSubmission =
            {
                ClientKind = RksvFinanzOnlineSubmissionClientKind.Real,
                AllowFakeClientInProduction = false,
            },
            GoLive =
            {
                AlertmanagerReceiversConfigured = true,
                AvvSignedForPilots = true,
                OnCallNamed = true,
                Section8Signed = true,
            },
        };
        return fixture;
    }

    private static (string Key, string? Value)[] SafeProductionPairs() =>
    [
        ("Security:Csrf:Enabled", "true"),
        ("FinanzOnline:Session:UseSimulation", "false"),
        ("FinanzOnline:Registrierkassen:UseSimulation", "false"),
        ("FinanzOnline:TransmissionQuery:UseSimulation", "false"),
        ("FinanzOnline:Mode", "Production"),
        ("RKSV:Mode", "Production"),
        ("RKSV:TseMode", "Device"),
        ("Backup:ExecutionAdapterKind", "PgDump"),
        ("PaymentGateway:Provider", "None"),
        ("TwoFactorAuth:Enabled", "true"),
        ("RateLimiting:Enabled", "true"),
        ("Redis:Enabled", "true"),
        ("Redis:ConnectionString", "redis:6379"),
    ];

    private static async Task SeedPassingEvidenceAsync(
        string dbName,
        bool includeSystemBackup = true,
        bool includeRestoreDrill = true,
        bool includeSignoff = true)
    {
        await using var db = CreateDb(dbName);
        if (includeSystemBackup)
        {
            db.BackupRuns.Add(new BackupRun
            {
                Id = Guid.NewGuid(),
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Scheduled,
                AdapterKind = "PgDump",
                Strategy = BackupStrategyKind.System,
                RequestedAt = DateTime.UtcNow.AddHours(-2),
                CompletedAt = DateTime.UtcNow.AddHours(-1),
            });
        }

        if (includeRestoreDrill)
        {
            db.RestoreVerificationRuns.Add(new RestoreVerificationRun
            {
                Id = Guid.NewGuid(),
                Status = RestoreVerificationStatus.Succeeded,
                TriggerSource = RestoreVerificationTriggerSource.Manual,
                RequestedAt = DateTime.UtcNow.AddHours(-3),
                CompletedAt = DateTime.UtcNow.AddHours(-2),
            });
        }

        if (includeSignoff)
        {
            db.DeploymentComplianceSignoffs.Add(new DeploymentComplianceSignoff
            {
                Id = Guid.NewGuid(),
                ImageTag = "sha-golive1",
                Stage = "production",
                ChecklistJson = "{}",
                SignedByUserId = "officer-1",
                SignedByDisplayName = "ComplianceOfficer",
                SignedAtUtc = DateTime.UtcNow.AddHours(-1),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(2),
            });
        }

        await db.SaveChangesAsync();
    }

    private static GoLiveCheckService CreateService(Fixture fixture, IGoLiveStatusStore? store = null)
    {
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(() => CreateDb(fixture.DbName));
        factory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CancellationToken _) => CreateDb(fixture.DbName));

        return new GoLiveCheckService(
            Config(fixture.ConfigPairs),
            Options.Create(fixture.Tse),
            Options.Create(fixture.Fiskaly),
            Options.Create(fixture.Backup),
            Options.Create(fixture.Monitoring),
            Options.Create(fixture.FonSubmission),
            Options.Create(fixture.GoLive),
            factory.Object,
            store ?? new GoLiveStatusStore(),
            NullLogger<GoLiveCheckService>.Instance);
    }

    private static IConfiguration Config(params (string Key, string? Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => p.Value))
            .Build();

    private static AppDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
    }

    private sealed class Fixture
    {
        public string DbName { get; } = $"GoLive_{Guid.NewGuid():N}";
        public (string Key, string? Value)[] ConfigPairs { get; set; } = [];
        public TseOptions Tse { get; } = new();
        public FiskalyOptions Fiskaly { get; } = new();
        public BackupOptions Backup { get; } = new();
        public MonitoringOptions Monitoring { get; } = new();
        public RksvFinanzOnlineSubmissionClientOptions FonSubmission { get; } = new();
        public GoLiveOptions GoLive { get; } = new();
    }
}
