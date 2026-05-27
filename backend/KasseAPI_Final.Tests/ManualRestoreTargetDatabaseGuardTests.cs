using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ManualRestoreTargetDatabaseGuardTests
{
    [Fact]
    public void ValidateOrThrow_rejects_non_validation_prefix()
    {
        var guard = CreateGuard(database: "regkasse_prod");
        var ex = Assert.Throws<ArgumentException>(() => guard.ValidateOrThrow("other_db"));
        Assert.Contains("restore_validation_", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateOrThrow_rejects_production_database_name()
    {
        var guard = CreateGuard(database: "regkasse_prod");
        var ex = Assert.Throws<ArgumentException>(() => guard.ValidateOrThrow("regkasse_prod"));
        Assert.Contains("blocked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateOrThrow_accepts_prefixed_isolated_name()
    {
        var guard = CreateGuard(database: "regkasse_prod");
        guard.ValidateOrThrow("restore_validation_20241231");
    }

    private static ManualRestoreTargetDatabaseGuard CreateGuard(string database)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    $"Host=localhost;Database={database};Username=u;Password=p"
            })
            .Build();

        var options = Options.Create(new ManualRestoreApprovalOptions
        {
            TargetDatabaseNamePrefix = "restore_validation_"
        });

        return new ManualRestoreTargetDatabaseGuard(config, new OptionsMonitorStub(options.Value));
    }

    private sealed class OptionsMonitorStub : IOptionsMonitor<ManualRestoreApprovalOptions>
    {
        public OptionsMonitorStub(ManualRestoreApprovalOptions value) => CurrentValue = value;
        public ManualRestoreApprovalOptions CurrentValue { get; }
        public ManualRestoreApprovalOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<ManualRestoreApprovalOptions, string?> listener) => null;
    }
}
