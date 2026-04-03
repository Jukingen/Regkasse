using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Kalıcı <c>evidence_json</c> sözleşmesi; UI’dan bağımsız makine okuması.
/// </summary>
public sealed class RestoreDrillEvidenceDocument
{
    public int SchemaVersion { get; init; } = 5;

    public DateTimeOffset CapturedAtUtc { get; init; }

    public Guid RestoreVerificationRunId { get; init; }

    public Guid? SourceBackupRunId { get; init; }

    public Guid? SourceBackupArtifactId { get; init; }

    public RestoreDrillValidityBands Validity { get; init; } = new();

    public List<RestoreDrillStageEvent> Stages { get; init; } = new();

    public PostRestoreSqlEvidenceBlock? PostRestoreContinuity { get; init; }

    public FiscalSqlEvidenceBlock? FiscalSql { get; init; }

    public LiveIntegrityEvidenceBlock? LiveOperationalIntegrity { get; init; }

    public ApplicationSmokeProbeEvidenceBlock? ApplicationSmokeProbe { get; init; }

    /// <summary>Geri yüklenen izole DB üzerinde in-process uygulama dumanı (L5 çekirdek).</summary>
    public RestoredDatabaseApplicationSmokeEvidenceBlock? RestoredDatabaseApplicationSmoke { get; init; }

    public ExternalDependencyRecoveryEvidenceBlock? ExternalDependencyRecovery { get; init; }
}

public sealed class RestoreDrillValidityBands
{
    public bool ArtifactResolved { get; init; }

    public bool? PgRestoreListArtifactReadable { get; init; }

    public bool? IsolatedRestoreMaterialized { get; init; }

    public bool? PostRestoreBusinessContinuitySqlPassed { get; init; }

    public bool? FiscalGoLiveScriptOnConfiguredConnectionPassed { get; init; }

    public bool? LiveOperationalIntegrityChecksPassed { get; init; }

    /// <summary>L4 bileşik: klon sürekliliği (kapsamdaysa) + fiscal betik geçti.</summary>
    public bool? FiscalContinuityLayerPassed { get; init; }

    /// <summary>L5a: geri yüklenen kopyaya karşı in-process duman (<see cref="RestoredDatabaseApplicationSmokeEvidenceBlock"/>).</summary>
    public RecoveryProofBand RestoredDatabaseApplicationSmoke { get; init; } = new()
    {
        Outcome = RecoveryProofOutcome.NotConfigured,
        Detail = "restored_database_application_smoke_not_attempted"
    };

    /// <summary>L5b: yapılandırılmış HTTP duman testi (ayrı taban URL; isteğe bağlı).</summary>
    public RecoveryProofBand ApplicationRecovery { get; init; } = new()
    {
        Outcome = RecoveryProofOutcome.NotConfigured,
        Detail = "application_smoke_probe_not_configured"
    };

    /// <summary>L6: kısmi — canlı harici API kanıtı değil.</summary>
    public RecoveryProofBand ExternalDependencyRecovery { get; init; } = new()
    {
        Outcome = RecoveryProofOutcome.Partial,
        Detail = "see_external_dependency_recovery_block"
    };
}

public sealed class RecoveryProofBand
{
    public RecoveryProofOutcome Outcome { get; init; }

    public string? Detail { get; init; }
}

public sealed class RestoreDrillStageEvent
{
    public RestoreDrillStage Stage { get; init; }

    public DateTimeOffset Utc { get; init; }

    public long? DurationMs { get; init; }

    public string? Note { get; init; }
}

public sealed class PostRestoreSqlEvidenceBlock
{
    /// <summary>Sabit kanıt katmanı kimliği (UI/JSON sözleşmesi).</summary>
    public string ProofLayerId { get; init; } = "L4_post_restore_continuity";

    /// <summary>L4 süreklilik SQL rollup (passed / failed / inconclusive / notExecuted).</summary>
    public PostRestoreContinuityProofState ContinuityChecksResult { get; init; }

    /// <summary>Kısa makine özeti (operatör + JSON tüketimi).</summary>
    public string? ContinuityChecksSummary { get; init; }

    public DateTimeOffset? StartedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public long? DurationMs { get; init; }

    public bool Executed { get; init; }

    public string? SkipReason { get; init; }

    public bool? Passed { get; init; }

    /// <summary>İlk required başarısızlığın sınıfı; başarılı koşuda null (eski kanıtta alan yok).</summary>
    public PostRestoreSqlFailureCategory? DominantFailureCategory { get; init; }

    public IReadOnlyList<PostRestoreSqlCheckRow> Checks { get; init; } = Array.Empty<PostRestoreSqlCheckRow>();
}

public sealed class PostRestoreSqlCheckRow
{
    /// <summary>Makine sabiti (ör. <c>fiscal_spine.receipts</c>).</summary>
    public string Id { get; init; } = "";

    /// <summary>Kısa insan okunur ad.</summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// <c>fiscal_spine</c> | <c>continuity_resilience</c> | <c>platform</c> |
    /// <c>fiscal_invariant</c> | <c>referential_integrity</c> | <c>informative</c>
    /// </summary>
    public string Category { get; init; } = "";

    public PostRestoreSqlCheckStatus Status { get; init; } = PostRestoreSqlCheckStatus.Passed;

    public PostRestoreSqlCheckSeverity Severity { get; init; } = PostRestoreSqlCheckSeverity.RequiredForL4;

    /// <summary>Makine sabiti (ör. <c>SCHEMA_RELATION_MISSING</c>).</summary>
    public string? ReasonCode { get; init; }

    /// <summary>Başarıda <see cref="PostRestoreSqlFailureCategory.None"/>; hata/anomali için sabit taksonomi (eski satırlarda null).</summary>
    public PostRestoreSqlFailureCategory? FailureCategory { get; init; }

    /// <summary>Kısa özet; UI ve log için.</summary>
    public string? Summary { get; init; }

    public long? MeasuredValue { get; init; }

    /// <summary>Beklenen alt sınır (ör. migration satırı ≥ 1).</summary>
    public long? ExpectedAtLeast { get; init; }

    /// <summary>Ek teknik ayrıntı (exception mesajı vb.).</summary>
    public string? Detail { get; init; }

    /// <summary>JSON uyumluluğu: <see cref="Status"/> ile türetilir.</summary>
    public bool Passed => Status == PostRestoreSqlCheckStatus.Passed;
}

public sealed class FiscalSqlEvidenceBlock
{
    public bool Executed { get; init; }

    public string? SkipReason { get; init; }

    public bool? Passed { get; init; }

    /// <summary>Betik hedefi: yapılandırılmış bağlantı adı; canlı DB ile aynı olabilir, geri yüklenen kopya olmak zorunda değil.</summary>
    public string? ValidityScopeNote { get; init; }
}

public sealed class LiveIntegrityEvidenceBlock
{
    public bool Executed { get; init; }

    public bool? Passed { get; init; }

    public string? Scope { get; init; }
}

public sealed class ApplicationSmokeProbeEvidenceBlock
{
    public bool Executed { get; init; }

    public bool? Passed { get; init; }

    public string? SkipReason { get; init; }

    /// <summary><c>passed</c> | <c>failed</c> (çalıştırıldıysa).</summary>
    public string? ApplicationSmokeResult { get; init; }

    public string? ApplicationSmokeSummary { get; init; }

    public DateTimeOffset? StartedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public int? HttpStatusCode { get; init; }

    public long? DurationMs { get; init; }

    public string? RequestPath { get; init; }

    public string? Error { get; init; }
}

/// <summary>Geri yüklenen izole PostgreSQL üzerinde EF + salt okunur tablo/okuma dumanı.</summary>
public sealed class RestoredDatabaseApplicationSmokeEvidenceBlock
{
    public bool Executed { get; init; }

    public RestoreDrillApplicationSmokeResultKind ResultKind { get; init; }

    /// <summary><c>passed</c> | <c>failed</c> | <c>not_attempted</c> | <c>not_supported</c> | <c>inconclusive</c> | <c>unknown</c>.</summary>
    public string? ApplicationSmokeResult { get; init; }

    public string? ApplicationSmokeSummary { get; init; }

    public string? Detail { get; init; }

    public long? DurationMs { get; init; }

    public DateTimeOffset? StartedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public IReadOnlyList<RestoredDatabaseApplicationSmokeCheckRow> Checks { get; init; } =
        Array.Empty<RestoredDatabaseApplicationSmokeCheckRow>();
}

/// <summary>L6 harici bağımlılık kurtarma kanıtı — alan bazlı durum + konsolide rollup.</summary>
public sealed class ExternalDependencyRecoveryEvidenceBlock
{
    /// <summary>L6 alt şema; <see cref="RestoreDrillEvidenceDocument.SchemaVersion"/> ile karıştırılmamalı.</summary>
    public int L6EvidenceSchemaVersion { get; init; } = 1;

    /// <summary>Konsolide makine durumu ve özet notlar.</summary>
    public ExternalDependencyProofRollup? Rollup { get; init; }

    /// <summary>TSE, sırlar, yedekleme araçları, arşiv, FinanzOnline vb. ayrı alanlar.</summary>
    public IReadOnlyList<ExternalDependencyDomainEvidence> Domains { get; init; } =
        Array.Empty<ExternalDependencyDomainEvidence>();

    /// <summary>Geriye dönük: <see cref="Rollup"/> ile <see cref="ExternalDependencyProofBandMapper.ToLegacyOverallOutcome"/> uyumlu tutulur.</summary>
    public RecoveryProofOutcome OverallOutcome { get; init; }

    public string? Interpretation { get; init; }

    /// <summary>Düz liste (UI/operatör); alan içi satırların birleşimi.</summary>
    public IReadOnlyList<ExternalDependencyCheckRow> Checks { get; init; } = Array.Empty<ExternalDependencyCheckRow>();
}

public sealed class ExternalDependencyProofRollup
{
    public ExternalDependencyProofState OverallState { get; init; }

    public string? Summary { get; init; }

    public string? Notes { get; init; }
}

public sealed class ExternalDependencyDomainEvidence
{
    public ExternalDependencyRecoveryDomain Domain { get; init; }

    public ExternalDependencyProofState State { get; init; }

    public string? Notes { get; init; }

    /// <summary>Kısa kod veya serbest metin (ör. <c>finanz_online_section_absent</c>).</summary>
    public string? Reason { get; init; }

    public IReadOnlyList<ExternalDependencyCheckRow> Checks { get; init; } = Array.Empty<ExternalDependencyCheckRow>();
}

public sealed class ExternalDependencyCheckRow
{
    public string Id { get; init; } = "";

    public bool Passed { get; init; }

    public string? Detail { get; init; }
}

/// <summary>Aşama zaman damgaları ve süreleri toplar.</summary>
public sealed class RestoreDrillEvidenceBuilder
{
    private DateTimeOffset _lastUtc = DateTimeOffset.UtcNow;

    public List<RestoreDrillStageEvent> Stages { get; } = new();

    public RestoreDrillStageEvent Mark(RestoreDrillStage stage, string? note = null)
    {
        var now = DateTimeOffset.UtcNow;
        var ms = (long)(now - _lastUtc).TotalMilliseconds;
        _lastUtc = now;
        var ev = new RestoreDrillStageEvent { Stage = stage, Utc = now, DurationMs = ms, Note = note };
        Stages.Add(ev);
        return ev;
    }
}

public static class RestoreDrillEvidenceJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(RestoreDrillEvidenceDocument doc) => JsonSerializer.Serialize(doc, Options);
}
