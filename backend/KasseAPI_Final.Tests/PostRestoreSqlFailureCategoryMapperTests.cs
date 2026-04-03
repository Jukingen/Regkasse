using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PostRestoreSqlFailureCategoryMapperTests
{
    [Theory]
    [InlineData(PostRestoreSqlReasonCodes.SchemaRelationMissing, PostRestoreSqlFailureCategory.MissingTable)]
    [InlineData(PostRestoreSqlReasonCodes.TableQueryFailed, PostRestoreSqlFailureCategory.QueryFailed)]
    [InlineData(PostRestoreSqlReasonCodes.OrphanReceiptItems, PostRestoreSqlFailureCategory.IntegrityCheckFailed)]
    [InlineData(PostRestoreSqlReasonCodes.UnhandledException, PostRestoreSqlFailureCategory.Unknown)]
    public void FromReasonCode_maps_known_codes_when_failed(string reason, PostRestoreSqlFailureCategory expected)
    {
        var actual = PostRestoreSqlFailureCategoryMapper.FromReasonCode(
            reason,
            PostRestoreSqlCheckStatus.Failed);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FromReasonCode_migration_insufficient_uses_table_suffix_for_migrations_history()
    {
        var m = PostRestoreSqlFailureCategoryMapper.FromReasonCode(
            PostRestoreSqlReasonCodes.MigrationHistoryInsufficient,
            PostRestoreSqlCheckStatus.Failed,
            "platform.__EFMigrationsHistory");
        Assert.Equal(PostRestoreSqlFailureCategory.MigrationHistoryMissing, m);

        var r = PostRestoreSqlFailureCategoryMapper.FromReasonCode(
            PostRestoreSqlReasonCodes.MigrationHistoryInsufficient,
            PostRestoreSqlCheckStatus.Failed,
            "other.table_name");
        Assert.Equal(PostRestoreSqlFailureCategory.RowCountUnexpected, r);
    }

    [Fact]
    public void ComputeDominantFailureCategory_first_required_failure_wins()
    {
        var checks = new PostRestoreSqlCheckRow[]
        {
            new()
            {
                Id = "fiscal_spine.receipts",
                Name = "r",
                Category = "fiscal_spine",
                Status = PostRestoreSqlCheckStatus.Failed,
                Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
                ReasonCode = PostRestoreSqlReasonCodes.SchemaRelationMissing,
                FailureCategory = PostRestoreSqlFailureCategory.MissingTable,
                Summary = "s"
            },
            new()
            {
                Id = "fiscal_spine.payment_details",
                Name = "p",
                Category = "fiscal_spine",
                Status = PostRestoreSqlCheckStatus.Failed,
                Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
                ReasonCode = PostRestoreSqlReasonCodes.SchemaRelationMissing,
                FailureCategory = PostRestoreSqlFailureCategory.MissingTable,
                Summary = "s2"
            }
        };

        Assert.Equal(PostRestoreSqlFailureCategory.MissingTable,
            PostRestoreSqlFailureCategoryMapper.ComputeDominantFailureCategory(checks));
    }

    [Fact]
    public void ComputeDominantFailureCategory_legacy_row_without_failureCategory_derives_from_reason()
    {
        var checks = new PostRestoreSqlCheckRow[]
        {
            new()
            {
                Id = "x.y",
                Name = "n",
                Category = "fiscal_spine",
                Status = PostRestoreSqlCheckStatus.Inconclusive,
                Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
                ReasonCode = PostRestoreSqlReasonCodes.TableQueryFailed,
                Summary = "s"
            }
        };

        Assert.Equal(PostRestoreSqlFailureCategory.QueryFailed,
            PostRestoreSqlFailureCategoryMapper.ComputeDominantFailureCategory(checks));
    }
}
