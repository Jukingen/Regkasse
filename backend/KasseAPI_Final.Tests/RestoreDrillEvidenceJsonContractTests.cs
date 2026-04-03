using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Kalıcı evidence_json sözleşmesi: şema 5, L6 rollup ve geçerlilik bantları (yanlış yeşil önleme).
/// </summary>
public sealed class RestoreDrillEvidenceJsonContractTests
{
    [Fact]
    public void Serialized_evidence_schema4_includes_validity_bands_and_l6_external_block()
    {
        var runId = Guid.NewGuid();
        var doc = new RestoreDrillEvidenceDocument
        {
            SchemaVersion = 5,
            CapturedAtUtc = DateTimeOffset.Parse("2026-04-02T12:00:00Z"),
            RestoreVerificationRunId = runId,
            SourceBackupRunId = Guid.NewGuid(),
            SourceBackupArtifactId = Guid.NewGuid(),
            Validity = new RestoreDrillValidityBands
            {
                ArtifactResolved = true,
                PgRestoreListArtifactReadable = true,
                FiscalContinuityLayerPassed = true,
                RestoredDatabaseApplicationSmoke = new RecoveryProofBand
                {
                    Outcome = RecoveryProofOutcome.NotConfigured,
                    Detail = "restored_database_application_smoke_not_attempted"
                },
                ApplicationRecovery = new RecoveryProofBand
                {
                    Outcome = RecoveryProofOutcome.NotConfigured,
                    Detail = "application_smoke_probe_disabled"
                },
                ExternalDependencyRecovery = new RecoveryProofBand
                {
                    Outcome = RecoveryProofOutcome.Partial,
                    Detail = "configuration_snapshot_only"
                }
            },
            Stages = new List<RestoreDrillStageEvent>(),
            ExternalDependencyRecovery = new ExternalDependencyRecoveryEvidenceBlock
            {
                L6EvidenceSchemaVersion = 1,
                Rollup = new ExternalDependencyProofRollup
                {
                    OverallState = ExternalDependencyProofState.NotProven,
                    Summary = "l6_test",
                    Notes = "n"
                },
                Domains = Array.Empty<ExternalDependencyDomainEvidence>(),
                OverallOutcome = RecoveryProofOutcome.Partial,
                Interpretation = "test",
                Checks = Array.Empty<ExternalDependencyCheckRow>()
            }
        };

        var json = RestoreDrillEvidenceJson.Serialize(doc);
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        Assert.Equal(5, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(runId, root.GetProperty("restoreVerificationRunId").GetGuid());

        var validity = root.GetProperty("validity");
        Assert.True(validity.GetProperty("fiscalContinuityLayerPassed").GetBoolean());
        Assert.Equal("notConfigured",
            validity.GetProperty("restoredDatabaseApplicationSmoke").GetProperty("outcome").GetString());
        Assert.Equal("notConfigured", validity.GetProperty("applicationRecovery").GetProperty("outcome").GetString());
        Assert.Equal("partial", validity.GetProperty("externalDependencyRecovery").GetProperty("outcome").GetString());

        Assert.Equal("partial", root.GetProperty("externalDependencyRecovery").GetProperty("overallOutcome").GetString());
        Assert.Equal(1, root.GetProperty("externalDependencyRecovery").GetProperty("l6EvidenceSchemaVersion").GetInt32());
        Assert.Equal("notProven",
            root.GetProperty("externalDependencyRecovery").GetProperty("rollup").GetProperty("overallState").GetString());
    }

    [Fact]
    public void PostRestoreSqlEvidenceBlock_serializes_L4_proof_layer_and_continuity_state()
    {
        var block = new PostRestoreSqlEvidenceBlock
        {
            ProofLayerId = "L4_post_restore_continuity",
            ContinuityChecksResult = PostRestoreContinuityProofState.Passed,
            ContinuityChecksSummary = "L4 continuity SQL: total=1, required_pass=1, required_failed=0, required_inconclusive=0, dominant_category=none, layer_pass=True",
            StartedAtUtc = DateTimeOffset.Parse("2026-04-02T12:00:01Z"),
            CompletedAtUtc = DateTimeOffset.Parse("2026-04-02T12:00:02Z"),
            DurationMs = 150,
            Executed = true,
            Passed = true,
            DominantFailureCategory = null,
            Checks =
            [
                new PostRestoreSqlCheckRow
                {
                    Id = "fiscal_spine.receipts",
                    Name = "Receipts",
                    Category = "fiscal_spine",
                    Status = PostRestoreSqlCheckStatus.Passed,
                    Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
                    ReasonCode = PostRestoreSqlReasonCodes.TableAccessible,
                    FailureCategory = PostRestoreSqlFailureCategory.None,
                    Summary = "Relation present and queryable; row_count=0.",
                    MeasuredValue = 0L
                }
            ]
        };

        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        var json = JsonSerializer.Serialize(block, opts);
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        Assert.Equal("L4_post_restore_continuity", root.GetProperty("proofLayerId").GetString());
        Assert.Equal("passed", root.GetProperty("continuityChecksResult").GetString());
        Assert.True(root.GetProperty("continuityChecksSummary").GetString()!.Contains("L4 continuity SQL", StringComparison.Ordinal));
        Assert.Equal(150, root.GetProperty("durationMs").GetInt64());
        Assert.True(root.GetProperty("executed").GetBoolean());
        Assert.True(root.GetProperty("passed").GetBoolean());
        var checks = root.GetProperty("checks");
        Assert.Equal(1, checks.GetArrayLength());
        var row = checks[0];
        Assert.Equal("fiscal_spine.receipts", row.GetProperty("id").GetString());
        Assert.Equal("passed", row.GetProperty("status").GetString());
        Assert.Equal("requiredForL4", row.GetProperty("severity").GetString());
        Assert.Equal("TABLE_ACCESSIBLE", row.GetProperty("reasonCode").GetString());
        Assert.Equal("none", row.GetProperty("failureCategory").GetString());
        Assert.True(row.GetProperty("passed").GetBoolean());
    }
}
