using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Artifact pipeline politikası: Development vs non-Development external archive, PgDump entegrasyonu.
/// Orchestrator ile aynı semantiği tek yerde tutar (drift önleme).
/// </summary>
public static class BackupArtifactPipelinePolicyEvaluator
{
    /// <summary>
    /// Staging artifact verification geçtikten sonra external copy çalıştırılmalı mı (orchestrator ile aynı koşul).
    /// </summary>
    public static bool ShouldRunExternalArchiveAfterStagingVerification(
        BackupExecutionAdapterKind adapterKind,
        IHostEnvironment environment,
        BackupOptions options) =>
        adapterKind == BackupExecutionAdapterKind.PgDump
        && (!environment.IsDevelopment() || !string.IsNullOrWhiteSpace(options.ExternalArchiveRoot));

    /// <summary>
    /// Admin / operasyon yüzeyi: config ile artifact pipeline beklentisini özetler (son run sonucu değil).
    /// </summary>
    public static BackupArtifactPipelinePolicySnapshot Evaluate(BackupOptions options, IHostEnvironment environment)
    {
        var dev = environment.IsDevelopment();
        var pg = options.ExecutionAdapterKind == BackupExecutionAdapterKind.PgDump;
        var extConfigured = !string.IsNullOrWhiteSpace(options.ExternalArchiveRoot);
        var stagingRootConfigured = !string.IsNullOrWhiteSpace(options.ArtifactStagingRoot);

        var requirement = options.ExecutionAdapterKind switch
        {
            BackupExecutionAdapterKind.PgDump when !dev => BackupExternalArchiveRequirementKind.RequiredForProductionLike,
            BackupExecutionAdapterKind.PgDump when dev && extConfigured => BackupExternalArchiveRequirementKind.OptionalButConfigured,
            BackupExecutionAdapterKind.PgDump when dev && !extConfigured => BackupExternalArchiveRequirementKind.SkippedInDevelopmentUnlessConfigured,
            _ => BackupExternalArchiveRequirementKind.NotApplicable
        };

        var willRunExternal = ShouldRunExternalArchiveAfterStagingVerification(
            options.ExecutionAdapterKind,
            environment,
            options);

        var stagingVerifyExpected = pg && options.VerifyLogicalDumpFileOnDisk;

        var lines = new List<string>();
        if (pg && !dev && !extConfigured)
            lines.Add("External archive root is required for PgDump outside Development.");
        if (pg && dev && !extConfigured)
            lines.Add("Development: external archive copy is skipped until Backup:ExternalArchiveRoot is set (health Degraded).");
        if (pg && !options.VerifyLogicalDumpFileOnDisk && !dev)
            lines.Add("Non-Development PgDump expects Backup:VerifyLogicalDumpFileOnDisk=true for production-safe verification.");

        if (pg && !dev && extConfigured && options.RequireExternalArchiveImmutableTarget && !options.ExternalArchiveImmutabilityAcknowledged)
            lines.Add(
                "DR readiness: Backup:RequireExternalArchiveImmutableTarget is true but ExternalArchiveImmutabilityAcknowledged is false — configuration is Unhealthy until the immutable archive tier is attested.");

        if (pg && !dev && extConfigured
            && !options.RequireExternalArchiveImmutableTarget
            && !options.ExternalArchiveImmutabilityAcknowledged
            && !options.ExternalArchiveMutableTargetAccepted)
            lines.Add(
                "DR readiness: external archive path is configured but operator disposition is missing — acknowledge immutable tier (Backup:ExternalArchiveImmutabilityAcknowledged), accept mutable target (Backup:ExternalArchiveMutableTargetAccepted), or require immutable posture (Backup:RequireExternalArchiveImmutableTarget + acknowledgment).");

        if (options.RetentionPolicyMode != BackupRetentionPolicyMode.Disabled && options.ArtifactRetentionDays.HasValue)
        {
            var readiness = BackupRetentionReadinessEvaluator.Build(options);
            lines.Add(
                $"Retention policy: mode={options.RetentionPolicyMode}, ArtifactRetentionDays={options.ArtifactRetentionDays.Value}, executableStatus={readiness.ExecutableStatus} (API does not delete artifacts; Backup:RetentionArtifactDeletionEnabled must remain false).");
        }

        return new BackupArtifactPipelinePolicySnapshot
        {
            ExternalArchiveRequirement = requirement,
            ExternalArchiveRootConfigured = extConfigured,
            ArtifactStagingRootConfigured = stagingRootConfigured,
            WillRunExternalArchiveAfterStagingVerificationWhenEligible = willRunExternal,
            StagingOnDiskHashReverificationExpected = stagingVerifyExpected,
            EffectiveAdapterKind = options.ExecutionAdapterKind,
            OperatorNotes = lines
        };
    }
}

/// <summary>Harici kopya zorunluluğu (config düzlemi).</summary>
public enum BackupExternalArchiveRequirementKind
{
    NotApplicable = 0,
    /// <summary>Development + PgDump; kök yoksa kopya atlanır (Degraded health).</summary>
    SkippedInDevelopmentUnlessConfigured = 1,
    /// <summary>Development ama kök tanımlı; doğrulama sonrası kopya çalışır.</summary>
    OptionalButConfigured = 2,
    /// <summary>Staging dışı PgDump; kök ve post-copy checksum zorunlu (Unhealthy eksikse).</summary>
    RequiredForProductionLike = 3
}

/// <summary>Artifact pipeline için salt-okunur config özeti (UI / API).</summary>
public sealed class BackupArtifactPipelinePolicySnapshot
{
    public BackupExternalArchiveRequirementKind ExternalArchiveRequirement { get; init; }

    public bool ExternalArchiveRootConfigured { get; init; }

    public bool ArtifactStagingRootConfigured { get; init; }

    /// <summary>Staging verification geçerse bir sonraki adımda external copy denenecek mi.</summary>
    public bool WillRunExternalArchiveAfterStagingVerificationWhenEligible { get; init; }

    /// <summary>BackupVerificationService disk üzerinde SHA-256 tekrar hesaplaması bekleniyor mu.</summary>
    public bool StagingOnDiskHashReverificationExpected { get; init; }

    public BackupExecutionAdapterKind EffectiveAdapterKind { get; init; }

    public IReadOnlyList<string> OperatorNotes { get; init; } = Array.Empty<string>();
}
