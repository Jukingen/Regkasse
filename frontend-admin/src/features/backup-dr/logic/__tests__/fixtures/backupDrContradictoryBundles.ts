/**
 * Belirleyici (deterministik) Backup & DR çelişkili durum paketleri — entegrasyon testlerinde
 * `buildBackupOperatorTruthModel` girdileri olarak kullanılır. Tek gerçek kaynak: buradaki fabrikalar.
 */
import type {
  BackupConfigurationHealthResponseDto,
  BackupRecoverabilitySummaryResponseDto,
  BackupRunResponseDto,
  BackupVerificationResponseDto,
  RestoreCapabilityDto,
  RestoreVerificationReadinessResponseDto,
  RestoreVerificationRunResponseDto,
} from '@/api/generated/model';
import {
  BackupRunResponseDtoStatus,
  BackupVerificationResponseDtoStatus,
  RestoreVerificationRunResponseDtoStatus,
} from '@/api/generated/model';
import { BackupArtifactResponseDtoArtifactType } from '@/api/generated/model/backupArtifactResponseDtoArtifactType';
import type { ExternalCopyVariant } from '@/features/backup-dr/logic/backupDrMappers';
import type { BuildBackupOperatorTruthModelParams } from '@/features/backup-dr/logic/backupDrOperatorTruthModel';
import type { BackupExecutionModeResponseDto } from '@/features/backup-dr/logic/backupExecutionModeApi';

export const identityT: BuildBackupOperatorTruthModelParams['t'] = (k) => k;

/** Gerçekçi minimum execution-mode gövdesi — alanlar bilinçli boş bırakılabilir. */
export function baseExecutionModeDto(
  over: Partial<BackupExecutionModeResponseDto> = {}
): BackupExecutionModeResponseDto {
  return {
    storedMode: 'InheritFromConfiguration',
    requestedUserFacingMode: 'UseConfigurationDefault',
    configurationDefaultUserFacingMode: 'Fake',
    effectiveUserFacingMode: 'Fake',
    recommendedFallbackUserFacingMode: null,
    adapterKindIfConfigurationDefaultOnly: 'Fake',
    effectiveModeResolutionSummaryEnglish: 'test-resolution',
    configurationExecutionAdapterKind: 'Fake',
    effectiveExecutionAdapterKind: 'Fake',
    effectiveModeRunnable: true,
    hypotheticalPgDumpHealthLevel: 'Healthy',
    blockers: [],
    realModeBlockingDiagnostics: [],
    selectableModes: [],
    effectiveConfigurationHealth: {},
    ...over,
  };
}

function baseParams(
  over: Partial<BuildBackupOperatorTruthModelParams>
): BuildBackupOperatorTruthModelParams {
  return {
    t: identityT,
    health: undefined,
    healthLv: '',
    restoreReady: undefined,
    restoreLv: '',
    latest: undefined,
    detailForPipeline: null,
    verification: undefined,
    restoreLatest: undefined,
    recoverabilitySummary: undefined,
    restoreCapability: undefined,
    externalCopyVariant: 'unknown',
    executionModeDto: baseExecutionModeDto(),
    hasStatusPayload: true,
    ...over,
  };
}

/** Senaryo: Son çalıştırma teknik başarılı (PgDump) ama özet kanıt zaman damgalarından biri eksik. */
export function bundleLatestSuccessWeakLastKnownGoodProof(): BuildBackupOperatorTruthModelParams {
  const latest = {
    id: 'run-latest-1',
    status: BackupRunResponseDtoStatus.NUMBER_3,
    adapterKind: 'PgDump',
  } as BackupRunResponseDto;

  return baseParams({
    health: { realPostgreSqlLogicalDumpConfigured: true } as BackupConfigurationHealthResponseDto,
    healthLv: 'healthy',
    restoreReady: {
      level: 'healthy',
      workerEnabled: true,
    } as RestoreVerificationReadinessResponseDto,
    restoreLv: 'healthy',
    latest,
    detailForPipeline: null,
    restoreLatest: {
      status: RestoreVerificationRunResponseDtoStatus.NUMBER_2,
      id: 'drill-ok',
    } as RestoreVerificationRunResponseDto,
    verification: {
      status: BackupVerificationResponseDtoStatus.NUMBER_1,
      backupRunId: 'run-latest-1',
    } as BackupVerificationResponseDto,
    recoverabilitySummary: {
      realPostgreSqlLogicalDumpConfigured: true,
      lastSuccessfulBackupAt: null,
      lastSuccessfulArtifactVerificationAt: '2026-01-02T00:00:00Z',
      lastSuccessfulRestoreProofAt: '2026-01-02T00:00:00Z',
    } as BackupRecoverabilitySummaryResponseDto,
    restoreCapability: { isAutomatedRestoreAvailable: true } as RestoreCapabilityDto,
    externalCopyVariant: 'staging' as ExternalCopyVariant,
    executionModeDto: baseExecutionModeDto({
      effectiveExecutionAdapterKind: 'PgDump',
      effectiveUserFacingMode: 'RealPgDump',
      configurationExecutionAdapterKind: 'PgDump',
      requestedUserFacingMode: 'RealPgDump',
    }),
  });
}

/** Senaryo: Özet kanıt tam; son restore tatbikatı API’de başarısız — başarı drill kanıtını geçersiz kılmamalı. */
export function bundleLatestSuccessFailedLatestDrill(): BuildBackupOperatorTruthModelParams {
  const latest = {
    id: 'run-good-1',
    status: BackupRunResponseDtoStatus.NUMBER_3,
    adapterKind: 'PgDump',
  } as BackupRunResponseDto;

  const detailWithLogicalDump = {
    id: 'run-good-1',
    status: BackupRunResponseDtoStatus.NUMBER_3,
    adapterKind: 'PgDump',
    artifacts: [
      {
        artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
        isFilePresentForDownload: true,
      },
    ],
  } as BackupRunResponseDto;

  return baseParams({
    health: { realPostgreSqlLogicalDumpConfigured: true } as BackupConfigurationHealthResponseDto,
    healthLv: 'healthy',
    restoreReady: {
      level: 'healthy',
      workerEnabled: true,
    } as RestoreVerificationReadinessResponseDto,
    restoreLv: 'healthy',
    latest,
    detailForPipeline: detailWithLogicalDump,
    verification: {
      status: BackupVerificationResponseDtoStatus.NUMBER_1,
      backupRunId: 'run-good-1',
    } as BackupVerificationResponseDto,
    restoreLatest: {
      status: RestoreVerificationRunResponseDtoStatus.NUMBER_3,
      failureCode: 'E_DRILL',
      failureDetail: 'drill boom',
    } as RestoreVerificationRunResponseDto,
    recoverabilitySummary: {
      realPostgreSqlLogicalDumpConfigured: true,
      lastSuccessfulBackupAt: '2026-01-01T00:00:00Z',
      lastSuccessfulArtifactVerificationAt: '2026-01-01T00:00:00Z',
      lastSuccessfulRestoreProofAt: '2026-01-01T00:00:00Z',
    } as BackupRecoverabilitySummaryResponseDto,
    restoreCapability: undefined,
    externalCopyVariant: 'unknown',
    executionModeDto: baseExecutionModeDto({
      effectiveExecutionAdapterKind: 'PgDump',
      effectiveUserFacingMode: 'RealPgDump',
      requestedUserFacingMode: 'RealPgDump',
    }),
  });
}

/** Senaryo: Fake başarı + API “healthy” özet — üst yüzey yeşil “güçlü kurtarma” olarak sunulmamalı. */
export function bundleSimulatedSuccessHealthyApiCapsReadiness(): BuildBackupOperatorTruthModelParams {
  const latest = {
    id: 'run-fake-1',
    status: BackupRunResponseDtoStatus.NUMBER_3,
    adapterKind: 'Fake',
  } as BackupRunResponseDto;

  return baseParams({
    health: {
      level: 'healthy',
      workerEnabled: true,
      effectiveAdapterKind: 'Fake',
      realPostgreSqlLogicalDumpConfigured: false,
      issues: [],
    } as BackupConfigurationHealthResponseDto,
    healthLv: 'healthy',
    restoreReady: {
      level: 'healthy',
      workerEnabled: true,
    } as RestoreVerificationReadinessResponseDto,
    restoreLv: 'healthy',
    latest,
    detailForPipeline: null,
    verification: undefined,
    restoreLatest: undefined,
    recoverabilitySummary: {
      realPostgreSqlLogicalDumpConfigured: false,
      lastSuccessfulBackupAt: '2026-01-01T00:00:00Z',
      lastSuccessfulArtifactVerificationAt: '2026-01-01T00:00:00Z',
      lastSuccessfulRestoreProofAt: '2026-01-01T00:00:00Z',
    } as BackupRecoverabilitySummaryResponseDto,
    restoreCapability: { isAutomatedRestoreAvailable: false } as RestoreCapabilityDto,
    externalCopyVariant: 'staging',
    executionModeDto: baseExecutionModeDto(),
  });
}

/** Senaryo: Harici aşama metadata’da “OK” + özet kanıt eksik — dış kopya tek başına kanıt değildir. */
export function bundleExternalLifecycleOkButRecoverabilityProofGaps(): BuildBackupOperatorTruthModelParams {
  const latest = {
    id: 'run-ext-1',
    status: BackupRunResponseDtoStatus.NUMBER_3,
    adapterKind: 'PgDump',
  } as BackupRunResponseDto;

  return baseParams({
    health: { realPostgreSqlLogicalDumpConfigured: true } as BackupConfigurationHealthResponseDto,
    healthLv: 'healthy',
    restoreReady: {
      level: 'healthy',
      workerEnabled: true,
    } as RestoreVerificationReadinessResponseDto,
    restoreLv: 'healthy',
    latest,
    detailForPipeline: null,
    verification: {
      status: BackupVerificationResponseDtoStatus.NUMBER_1,
      backupRunId: 'run-ext-1',
    } as BackupVerificationResponseDto,
    restoreLatest: {
      status: RestoreVerificationRunResponseDtoStatus.NUMBER_2,
    } as RestoreVerificationRunResponseDto,
    recoverabilitySummary: {
      realPostgreSqlLogicalDumpConfigured: true,
      lastSuccessfulBackupAt: '2026-01-01T00:00:00Z',
      lastSuccessfulArtifactVerificationAt: null,
      lastSuccessfulRestoreProofAt: '2026-01-01T00:00:00Z',
    } as BackupRecoverabilitySummaryResponseDto,
    restoreCapability: undefined,
    externalCopyVariant: 'externalLifecycleOk' as ExternalCopyVariant,
    executionModeDto: baseExecutionModeDto({
      effectiveExecutionAdapterKind: 'PgDump',
      effectiveUserFacingMode: 'RealPgDump',
      requestedUserFacingMode: 'RealPgDump',
    }),
  });
}

/** Senaryo: Hem kanıt boşluğu hem son tatbikat başarısız — şerit önceliği + banner kritik sinyal ayrımı. */
export function bundleProofGapsAndFailedDrill(): BuildBackupOperatorTruthModelParams {
  const latest = {
    id: 'run-both-1',
    status: BackupRunResponseDtoStatus.NUMBER_3,
    adapterKind: 'PgDump',
  } as BackupRunResponseDto;

  const detailWithLogicalDump = {
    id: 'run-both-1',
    status: BackupRunResponseDtoStatus.NUMBER_3,
    adapterKind: 'PgDump',
    artifacts: [
      {
        artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
        isFilePresentForDownload: true,
      },
    ],
  } as BackupRunResponseDto;

  return baseParams({
    health: { realPostgreSqlLogicalDumpConfigured: true } as BackupConfigurationHealthResponseDto,
    healthLv: 'healthy',
    restoreReady: {
      level: 'healthy',
      workerEnabled: true,
    } as RestoreVerificationReadinessResponseDto,
    restoreLv: 'healthy',
    latest,
    detailForPipeline: detailWithLogicalDump,
    verification: {
      status: BackupVerificationResponseDtoStatus.NUMBER_1,
      backupRunId: 'run-both-1',
    } as BackupVerificationResponseDto,
    restoreLatest: {
      status: RestoreVerificationRunResponseDtoStatus.NUMBER_3,
      failureCode: 'E_FAIL',
      failureDetail: 'x',
    } as RestoreVerificationRunResponseDto,
    recoverabilitySummary: {
      realPostgreSqlLogicalDumpConfigured: true,
      lastSuccessfulBackupAt: null,
      lastSuccessfulArtifactVerificationAt: '2026-01-01T00:00:00Z',
      lastSuccessfulRestoreProofAt: '2026-01-01T00:00:00Z',
    } as BackupRecoverabilitySummaryResponseDto,
    restoreCapability: undefined,
    externalCopyVariant: 'staging',
    executionModeDto: baseExecutionModeDto({
      effectiveExecutionAdapterKind: 'PgDump',
      effectiveUserFacingMode: 'RealPgDump',
      requestedUserFacingMode: 'RealPgDump',
    }),
  });
}

/** Senaryo: Son doğrulama satırı farklı runId — “son çalıştırma” ile hizasız pencere. */
export function bundleVerificationRunMismatch(): BuildBackupOperatorTruthModelParams {
  const latest = {
    id: 'run-new',
    status: BackupRunResponseDtoStatus.NUMBER_3,
    adapterKind: 'PgDump',
  } as BackupRunResponseDto;

  return baseParams({
    health: { realPostgreSqlLogicalDumpConfigured: true } as BackupConfigurationHealthResponseDto,
    healthLv: 'healthy',
    restoreReady: {
      level: 'healthy',
      workerEnabled: true,
    } as RestoreVerificationReadinessResponseDto,
    restoreLv: 'healthy',
    latest,
    detailForPipeline: null,
    verification: {
      status: BackupVerificationResponseDtoStatus.NUMBER_1,
      backupRunId: 'run-old',
    } as BackupVerificationResponseDto,
    restoreLatest: undefined,
    recoverabilitySummary: {
      realPostgreSqlLogicalDumpConfigured: true,
      lastSuccessfulBackupAt: '2026-01-01T00:00:00Z',
      lastSuccessfulArtifactVerificationAt: '2026-01-01T00:00:00Z',
      lastSuccessfulRestoreProofAt: '2026-01-01T00:00:00Z',
    } as BackupRecoverabilitySummaryResponseDto,
    restoreCapability: undefined,
    externalCopyVariant: 'staging',
    executionModeDto: baseExecutionModeDto({
      effectiveExecutionAdapterKind: 'PgDump',
      effectiveUserFacingMode: 'RealPgDump',
      requestedUserFacingMode: 'RealPgDump',
    }),
  });
}

/** Senaryo: Etkin adaptör türü boş string — kısmi DTO; simüle/PgDump bayrakları nötr. */
export function bundleEmptyEffectiveAdapterKind(): BuildBackupOperatorTruthModelParams {
  const latest = {
    id: 'run-unk',
    status: BackupRunResponseDtoStatus.NUMBER_3,
    adapterKind: undefined,
  } as BackupRunResponseDto;

  return baseParams({
    health: { realPostgreSqlLogicalDumpConfigured: true } as BackupConfigurationHealthResponseDto,
    healthLv: 'healthy',
    restoreReady: {
      level: 'healthy',
      workerEnabled: true,
    } as RestoreVerificationReadinessResponseDto,
    restoreLv: 'healthy',
    latest,
    detailForPipeline: null,
    verification: undefined,
    restoreLatest: undefined,
    recoverabilitySummary: {
      realPostgreSqlLogicalDumpConfigured: true,
      lastSuccessfulBackupAt: '2026-01-01T00:00:00Z',
      lastSuccessfulArtifactVerificationAt: '2026-01-01T00:00:00Z',
      lastSuccessfulRestoreProofAt: '2026-01-01T00:00:00Z',
    } as BackupRecoverabilitySummaryResponseDto,
    restoreCapability: undefined,
    externalCopyVariant: 'unknown',
    executionModeDto: baseExecutionModeDto({
      effectiveExecutionAdapterKind: '',
      configurationExecutionAdapterKind: '',
      adapterKindIfConfigurationDefaultOnly: '',
    }),
  });
}

/**
 * Senaryo: API bilinmeyen adaptör dizesi + eksik alanlar — Fake/PgDump çıkarımı yapılmaz; kısmi DTO ile nötr yol.
 */
export function bundleUnknownAdapterKindPartialDto(): BuildBackupOperatorTruthModelParams {
  const latest = {
    id: 'run-unknown-adapter-1',
    status: BackupRunResponseDtoStatus.NUMBER_3,
    adapterKind: 'VendorExperimental_Unknown',
    isSimulatedExecution: undefined,
  } as BackupRunResponseDto;

  return baseParams({
    health: {
      level: 'healthy',
      effectiveAdapterKind: 'VendorExperimental_Unknown',
      realPostgreSqlLogicalDumpConfigured: true,
    } as BackupConfigurationHealthResponseDto,
    healthLv: 'healthy',
    restoreReady: {
      level: 'healthy',
      workerEnabled: true,
    } as RestoreVerificationReadinessResponseDto,
    restoreLv: 'healthy',
    latest,
    detailForPipeline: null,
    verification: undefined,
    restoreLatest: undefined,
    recoverabilitySummary: {
      realPostgreSqlLogicalDumpConfigured: true,
      lastSuccessfulBackupAt: '2026-01-01T00:00:00Z',
      lastSuccessfulArtifactVerificationAt: '2026-01-01T00:00:00Z',
      lastSuccessfulRestoreProofAt: '2026-01-01T00:00:00Z',
    } as BackupRecoverabilitySummaryResponseDto,
    restoreCapability: undefined,
    externalCopyVariant: 'unknown',
    executionModeDto: baseExecutionModeDto({
      effectiveExecutionAdapterKind: 'PgDump',
      effectiveUserFacingMode: 'RealPgDump',
      requestedUserFacingMode: 'RealPgDump',
    }),
  });
}

/**
 * Senaryo: Liste “son çalıştırma” run A; detay yanıtı hâlâ run B (poll gecikmesi) — dosya varlığı B’den okunur.
 * Regresyon: tek başına dosya “pass” ile uçtan uca güven üretilmesin; kanıt merdiveni + özet şerit yine sınırlayıcı kalsın.
 */
export function bundleStaleLatestVersusDetailRunId(): BuildBackupOperatorTruthModelParams {
  const latest = {
    id: 'run-latest-window-a',
    status: BackupRunResponseDtoStatus.NUMBER_3,
    adapterKind: 'PgDump',
    requestedAt: '2026-01-10T12:00:00Z',
    completedAt: '2026-01-10T12:10:00Z',
  } as BackupRunResponseDto;

  const detailStaleOtherRun = {
    id: 'run-detail-window-b',
    status: BackupRunResponseDtoStatus.NUMBER_3,
    adapterKind: 'PgDump',
    requestedAt: '2026-01-01T00:00:00Z',
    completedAt: '2026-01-01T00:15:00Z',
    artifacts: [
      {
        artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
        isFilePresentForDownload: true,
      },
    ],
  } as BackupRunResponseDto;

  return baseParams({
    health: { realPostgreSqlLogicalDumpConfigured: true } as BackupConfigurationHealthResponseDto,
    healthLv: 'healthy',
    restoreReady: {
      level: 'healthy',
      workerEnabled: true,
    } as RestoreVerificationReadinessResponseDto,
    restoreLv: 'healthy',
    latest,
    detailForPipeline: detailStaleOtherRun,
    verification: {
      status: BackupVerificationResponseDtoStatus.NUMBER_1,
      backupRunId: 'run-latest-window-a',
    } as BackupVerificationResponseDto,
    restoreLatest: {
      status: RestoreVerificationRunResponseDtoStatus.NUMBER_2,
      id: 'drill-ok',
      pgRestoreListExitCode: 0,
    } as RestoreVerificationRunResponseDto,
    recoverabilitySummary: {
      realPostgreSqlLogicalDumpConfigured: true,
      lastSuccessfulBackupAt: '2026-01-01T00:00:00Z',
      lastSuccessfulArtifactVerificationAt: '2026-01-01T00:00:00Z',
      lastSuccessfulRestoreProofAt: '2026-01-01T00:00:00Z',
    } as BackupRecoverabilitySummaryResponseDto,
    restoreCapability: undefined,
    externalCopyVariant: 'staging',
    executionModeDto: baseExecutionModeDto({
      effectiveExecutionAdapterKind: 'PgDump',
      effectiveUserFacingMode: 'RealPgDump',
      requestedUserFacingMode: 'RealPgDump',
    }),
  });
}
