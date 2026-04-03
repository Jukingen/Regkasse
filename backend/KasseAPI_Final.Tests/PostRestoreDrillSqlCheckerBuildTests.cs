using KasseAPI_Final.Services.RestoreVerification;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Post-restore SQL denetçisi için bağlantı dizesi ve statik sözleşme testleri.
/// </summary>
public sealed class PostRestoreDrillSqlCheckerBuildTests
{
    [Fact]
    public void BuildTargetDatabaseConnectionString_sets_database_and_preserves_host_user()
    {
        const string admin =
            "Host=localhost;Port=5432;Username=u;Password=p;Database=postgres";
        var target = PostRestoreDrillSqlChecker.BuildTargetDatabaseConnectionString(admin, "rv_ephemeral_1");

        Assert.Contains("Database=rv_ephemeral_1", target);
        Assert.Contains("Host=localhost", target);
        Assert.Contains("Username=u", target);
    }
}
