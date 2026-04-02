/**
 * Backup & DR — backend sözleşmesi sertleştirmesi için genişletme noktaları ve sezgisel geri dönüş belgesi.
 * Bu dosya çalışma zamanı davranışını değiştirmez; `truthProvenance` ile hangi alanların hâlâ çıkarım
 * kullandığını tek yerde toplar. Gelecekte OpenAPI’ye eklenecek alan adları burada öneri olarak tutulur.
 */

import type { BackupExecutionModeTruth } from '@/features/backup-dr/logic/backupDrExecutionModeTruth';
import type {
  ArtifactTruth,
  RecoverabilityTruth,
  RestoreTruth,
  RunTruth,
} from '@/features/backup-dr/logic/backupDrOperatorTruthModel';

/**
 * İleride backend’den gelen “açık” alanlar — şu an Orval DTO’larında yok; istemci tarafında rezerve.
 * Yeni alanlar eklendiğinde `BuildBackupOperatorTruthModelParams.truthContractHints` ile geçirilebilir
 * (şimdilik hiçbir yerde okunmaz; büyük refactor öncesi tip sözleşmesi).
 */
export interface BackupTruthContractHints {
  /** Örnek: 'production_logical_dump' | 'simulated_pipeline' | 'stub_integration' */
  backupOperationalSurfaceKind?: string;
  /** Örnek: 'complete' | 'partial' | 'unknown' — özet kanıt zaman damgaları yerine tek alan. */
  recoverabilityProofCompleteness?: string;
  /** Harici arşivin kanıt gücü — lifecycle metadata ile karıştırılmaz. */
  externalArchiveProofKind?: string;
  /** Pipeline adımlarının güvenilirliği; sunucu projeksiyonu ile istemci tahminini ayırır. */
  pipelineTrust?: string;
  /** Restore tatbikatı hata sınıfı — `failureCode` + stderr çıkarımı yerine. */
  restoreFailureClassification?: string;
}

/** Tek bir mantıksal alan için: API mi, sezgisel mi, yoksa ikisi mi. */
export type TruthResolutionMode = 'api' | 'heuristic' | 'hybrid';

export interface TruthZoneProvenance {
  resolutionMode: TruthResolutionMode;
  /** Mevcut davranışın kısa özeti (operatör/debug). */
  summary: string;
  /** OpenAPI’de önerilen veya beklenen alan adları — yalnızca yönlendirme. */
  suggestedBackendFields: readonly string[];
}

export interface BackupOperatorTruthProvenance {
  simulatedOperationalSurface: TruthZoneProvenance & {
    runSimulatedSource: RunTruth['simulatedEvidenceSource'];
    executionModeLoaded: boolean;
  };
  restoreFailureAndDrill: TruthZoneProvenance & {
    latestDrillFailedFromStatusEnum: boolean;
  };
  recoverabilityProofStrength: TruthZoneProvenance & {
    hasProofGapsFromTimestamps: boolean;
  };
  externalArchiveProofStrength: TruthZoneProvenance & {
    variantKind: ArtifactTruth['externalArchiveTruthKind'];
  };
  pipelineConfidence: TruthZoneProvenance;
  readinessLevelCap: TruthZoneProvenance;
}

function zone(
  resolutionMode: TruthResolutionMode,
  summary: string,
  suggestedBackendFields: readonly string[],
): TruthZoneProvenance {
  return { resolutionMode, summary, suggestedBackendFields };
}

/**
 * Mevcut model parçalarından salt okunur bir “kanıt kaynağı” özeti üretir — UI’da göstermek zorunlu değil.
 */
export function buildBackupOperatorTruthProvenance(input: {
  run: RunTruth;
  recoverability: RecoverabilityTruth;
  artifact: ArtifactTruth;
  restore: RestoreTruth;
  executionMode: BackupExecutionModeTruth;
  readinessCapped: boolean;
}): BackupOperatorTruthProvenance {
  const { run, recoverability, artifact, restore, executionMode, readinessCapped } = input;

  const simulatedSurface: BackupOperatorTruthProvenance['simulatedOperationalSurface'] = {
    ...zone(
      run.simulatedEvidenceSource === 'api_flag'
        ? 'hybrid'
        : run.simulatedEvidence
          ? 'heuristic'
          : 'api',
      run.simulatedEvidence
        ? run.simulatedEvidenceSource === 'api_flag'
          ? 'Simülasyon: isSimulatedExecution API bayrağı.'
          : 'Simülasyon: Fake/ProductionStub adaptör adı çıkarımı (adapter_inference).'
        : 'Simüle kanıt yok; adaptör adı Fake/Stub değil veya bayrak false.',
      [
        'BackupRun.operationalSurfaceKind',
        'BackupRun.productionEvidenceKind',
        'BackupRun.isSimulatedExecution (mevcut, öncelikli)',
      ],
    ),
    runSimulatedSource: run.simulatedEvidenceSource,
    executionModeLoaded: executionMode.loaded,
  };

  const restoreZone: BackupOperatorTruthProvenance['restoreFailureAndDrill'] = {
    ...zone(
      'hybrid',
      'Drill başarı/başarısızlığı RestoreVerificationRunResponseDto.status enum ile; PG_RESTORE_LIST_FAILED için failureCode + stderr/çıkarım (interpretPgRestoreListFailure).',
      [
        'RestoreVerificationRun.failureClassification',
        'RestoreVerificationRun.failureTier',
        'RestoreVerificationRun.dumpInspectionClassification',
      ],
    ),
    latestDrillFailedFromStatusEnum: restore.latestDrillFailed,
  };

  const recoverabilityZone: BackupOperatorTruthProvenance['recoverabilityProofStrength'] = {
    ...zone(
      'heuristic',
      'Kanıt boşluğu: lastSuccessfulBackupAt / lastSuccessfulArtifactVerificationAt / lastSuccessfulRestoreProofAt varlığına (hasRecoverabilityProofGaps).',
      [
        'BackupRecoverabilitySummary.proofCompleteness',
        'BackupRecoverabilitySummary.proofStrength',
        'BackupRecoverabilitySummary.proofGaps[]',
      ],
    ),
    hasProofGapsFromTimestamps: recoverability.hasProofGaps,
  };

  const externalZone: BackupOperatorTruthProvenance['externalArchiveProofStrength'] = {
    ...zone(
      'heuristic',
      'Harici kart: artifact lifecycleState’lerinin istemci tarafında toplanması (mapArtifactsToExternalCopyVariant); externalArchiveTruthKind = frontend_inferred.',
      [
        'BackupRun.externalArchiveProofKind',
        'BackupArtifactSummary.externalCopyProof',
        'BackupRun.latestArtifacts[].externalProofKind',
      ],
    ),
    variantKind: artifact.externalArchiveTruthKind,
  };

  const pipelineZone: BackupOperatorTruthProvenance['pipelineConfidence'] = zone(
    'heuristic',
    'Operatör truth modeli pipeline adımı üretmez; BackupStatusCard → resolveBackupPipelineStepsForUi (sunucu snapshot veya istemci fallback).',
    [
      'BackupPipelineSnapshotDto.confidence',
      'BackupPipelineSnapshotDto.source',
      'BackupRun.pipelineProvenance',
    ],
  );

  const readinessCap: BackupOperatorTruthProvenance['readinessLevelCap'] = {
    ...zone(
      'heuristic',
      readinessCapped
        ? 'Restore readiness seviyesi: computeEffectiveRestoreReadinessLevel ile API seviyesi tavanlandı (pg_dump yok / simüle çalıştırma / executionMode Fake).'
        : 'Restore readiness: API seviyesi ile etkin seviye aynı (tavan yok).',
      [
        'RestoreVerificationReadinessResponseDto.effectiveLevel',
        'RestoreVerificationReadinessResponseDto.capReason',
        'BackupConfigurationHealthResponseDto.realDumpOperationalSignals',
      ],
    ),
  };

  return {
    simulatedOperationalSurface: simulatedSurface,
    restoreFailureAndDrill: restoreZone,
    recoverabilityProofStrength: recoverabilityZone,
    externalArchiveProofStrength: externalZone,
    pipelineConfidence: pipelineZone,
    readinessLevelCap: readinessCap,
  };
}
