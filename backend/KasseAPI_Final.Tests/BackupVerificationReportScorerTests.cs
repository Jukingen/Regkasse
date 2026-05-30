using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupVerificationReportScorerTests
{
    [Fact]
    public void CalculateScore_perfect_dump_and_counts_returns_100()
    {
        var monitored = new[] { "products", "categories" };
        var dump = new HashSet<string>(monitored, StringComparer.OrdinalIgnoreCase);
        var source = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            ["products"] = 10,
            ["categories"] = 5,
        };

        var score = BackupVerificationReportScorer.CalculateScore(
            logicalDumpAnalyzed: true,
            monitored,
            dump,
            source,
            source);

        Assert.Equal(100, score);
        Assert.Equal("Verified", BackupVerificationReportScorer.MapStatus(score));
    }

    [Fact]
    public void CalculateScore_missing_table_in_dump_reduces_score()
    {
        var monitored = new[] { "products", "categories" };
        var dump = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "products" };
        var source = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            ["products"] = 10,
            ["categories"] = 5,
        };

        var score = BackupVerificationReportScorer.CalculateScore(
            true,
            monitored,
            dump,
            source,
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["products"] = 10 });

        Assert.True(score < 100);
    }
}
