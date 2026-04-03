/**
 * DR kanıt seviyesi (L0–L6): tek merkezden türetilir; kartlar çekirdek anlamı yeniden yorumlamaz.
 * Simüle/stub geçmişi yüksek seviye sayılmaz; API’de olmayan uçlar açıkça "not proven" kalır.
 */

import type {
  BackupRecoverabilitySummaryResponseDto,
  BackupRunResponseDto,
  BackupVerificationResponseDto,
  RestoreVerificationRunResponseDto,
} from '@/api/generated/model';
import {
  BackupArtifactResponseDtoArtifactType,
  BackupRunResponseDtoStatus,
  RestoreVerificationRunResponseDtoStatus,
} from '@/api/generated/model';
import { mapDumpInspectionTriState } from '@/features/backup-dr/logic/backupDrMappers';
import type { BackupOperatorTruthModel } from '@/features/backup-dr/logic/backupDrOperatorTruthModel';
import type { RestoreVerificationRunDtoExtended } from '@/features/backup-dr/logic/backupDrRestoreRunDtoCompat';

/** Sunucu sözleşmesiyle hizalı seviye kimlikleri (0–6). */
export const DR_PROOF_LEVEL_IDS = ['L0', 'L1', 'L2', 'L3', 'L4', 'L5', 'L6'] as const;
export type DrProofLevelIndex = 0 | 1 | 2 | 3 | 4 | 5 | 6;

export type DrProofLayerUiState = 'proven' | 'partial' | 'gap' | 'stub_only' | 'not_applicable' | 'unknown';

export interface DrProofLayerRow {
  id: DrProofLevelIndex;
  /** i18n anahtarı — başlık */
  titleKey: string;
  /** i18n anahtarı — açıklama */
  detailKey: string;
  state: DrProofLayerUiState;
}

export interface DrProofPresentationModel {
  /** Operatörün gördüğü en yüksek tam kanıt seviyesi (0–6, muhafazakâr). */
  highestFullyProvenLevel: DrProofLevelIndex;
  /** Sunulan katman satırları (L0–L6). */
  layers: DrProofLayerRow[];
  /** Bir sonraki olası iyileştirme (i18n). */
  nextStepKey: string;
  /** Üst şerit: asla "healthy" genellemesi yok; kanıt sınıfına göre. */
  decisionStrip: {
    alertType: 'success' | 'info' | 'warning' | 'error';
    titleKey: string;
    bodyKey: string;
    /** Son drill başarısızsa üst mesajları baskılar. */
    suppressOptimisticTone: boolean;
  };
  /** Son başarılı gerçek yedek çalıştırma (stub değil) — özet kartı. */
  latestRealBackupArtifactSummary: {
    labelKey: string;
    runId?: string;
    isDistinctFromLatestRequest: boolean;
  };
  /** Restore drill kaynağı (stub hariç kanıt). */
  latestRestoreVerifiedSummary: {
    labelKey: string;
    sourceBackupRunId?: string;
    sourceArtifactId?: string;
    drillSucceeded: boolean;
  };
  fiscalVerifiedSummary: {
    labelKey: string;
    passed?: boolean;
    skipped?: boolean;
  };
  appRecoverySummary: {
    labelKey: string;
    state: 'proven' | 'failed' | 'not_proven' | 'not_configured';
  };
  externalDepsSummary: {
    labelKey: string;
    state: 'partial_configuration' | 'not_proven';
  };
}

function logicalDumpPass(latest: BackupRunResponseDto | undefined, detail: BackupRunResponseDto | null | undefined): boolean {
  const arts = detail?.artifacts ?? latest?.artifacts ?? [];
  const row = arts.find((a) => a.artifactType === BackupArtifactResponseDtoArtifactType.NUMBER_0);
  if (row?.isFilePresentForDownload === true) return true;
  return false;
}

function verificationPass(verification: BackupVerificationResponseDto | undefined, latestRunId: string | undefined): boolean {
  if (!verification || verification.status === undefined || verification.status === null) return false;
  if (verification.status !== 1) return false;
  const bid = verification.backupRunId?.trim();
  if (latestRunId && bid && bid !== latestRunId) return false;
  return true;
}

function drillSucceeded(restore: RestoreVerificationRunResponseDto | undefined): boolean {
  return restore?.status === RestoreVerificationRunResponseDtoStatus.NUMBER_2;
}

function fiscalProven(restore: RestoreVerificationRunResponseDto | undefined): boolean {
  if (!restore || !drillSucceeded(restore)) return false;
  if (restore.fiscalSqlSkipped) return false;
  return restore.fiscalSqlPassed === true;
}

/** L4: izole geri yükleme sonrası süreklilik SQL (L3 üzerine). */
function postRestoreContinuitySqlProven(extended: RestoreVerificationRunDtoExtended): boolean {
  return extended.postRestoreContinuityChecksExecuted === true && extended.postRestoreContinuityChecksPassed === true;
}

/** Bileşik “fiscal continuity layer” (API bayrağı veya fiscal betik). Ayrı kartta gösterilir; L4 basamağı SQL ile hizalanır. */
function fiscalContinuityProven(
  restore: RestoreVerificationRunResponseDto | undefined,
  extended: RestoreVerificationRunDtoExtended,
): boolean {
  if (!restore || !drillSucceeded(restore)) return false;
  if (typeof extended.fiscalContinuityLayerPassed === 'boolean') return extended.fiscalContinuityLayerPassed;
  return fiscalProven(restore);
}

/** L5: geri yüklenen DB üzerinde in-process duman (L5a). */
function restoredDbApplicationSmokeProven(extended: RestoreVerificationRunDtoExtended): boolean {
  return (
    extended.restoredDatabaseApplicationSmokeExecuted === true && extended.restoredDatabaseApplicationSmokePassed === true
  );
}

function applicationSmokeProven(
  restore: RestoreVerificationRunResponseDto | undefined,
  extended: RestoreVerificationRunDtoExtended,
): boolean {
  if (!restore || !drillSucceeded(restore)) return false;
  return extended.applicationSmokeProbeExecuted === true && extended.applicationSmokeProbePassed === true;
}

function restoreLevel3Proven(
  restore: RestoreVerificationRunResponseDto | undefined,
  extended: RestoreVerificationRunDtoExtended,
  dumpListOk: boolean,
): { full: boolean; partial: boolean } {
  if (!restore || !drillSucceeded(restore)) return { full: false, partial: false };
  if (!dumpListOk) return { full: false, partial: true };
  if (restore.restoreAttemptExecuted === true) {
    const iso = restore.restoreAttemptPassed === true;
    const post =
      extended.postRestoreContinuityChecksExecuted === true ? extended.postRestoreContinuityChecksPassed === true : true;
    return { full: iso && post, partial: iso || extended.postRestoreContinuityChecksPassed === true };
  }
  /* İzole restore çalıştırılmadı — tam L3 değil */
  return { full: false, partial: true };
}

/**
 * Merkezi DR kanıt görünümü — `BackupDrDashboard` tek kaynaktan besler.
 */
export function buildDrProofPresentationModel(params: {
  truth: BackupOperatorTruthModel;
  latest: BackupRunResponseDto | undefined;
  detailForPipeline: BackupRunResponseDto | null | undefined;
  verification: BackupVerificationResponseDto | undefined;
  recoverability: BackupRecoverabilitySummaryResponseDto | undefined;
  restoreLatest: RestoreVerificationRunResponseDto | undefined;
  restoreExtended: RestoreVerificationRunDtoExtended;
}): DrProofPresentationModel {
  const { truth, latest, detailForPipeline, verification, recoverability, restoreLatest, restoreExtended } = params;

  const simulated = truth.run.simulatedEvidence;
  const technicalOk = latest?.status === BackupRunResponseDtoStatus.NUMBER_3;
  const latestId = latest?.id?.trim();
  const dumpListOk = mapDumpInspectionTriState(restoreLatest) === true;
  const l3 = restoreLevel3Proven(restoreLatest, restoreExtended, dumpListOk);

  const l0 =
    Boolean(recoverability?.lastSuccessfulBackupAt) ||
    Boolean(recoverability?.latestRunAt) ||
    Boolean(latest) ||
    technicalOk;

  const l1 = !simulated && technicalOk && logicalDumpPass(latest, detailForPipeline) && truth.run.realPostgreSqlLogicalDumpConfigured === true;

  const l2 = l1 && verificationPass(verification, latestId);

  const l3Full = !simulated && l3.full;
  const l3Partial = !simulated && (l3.partial || (drillSucceeded(restoreLatest) && !l3.full));

  const l4 = !simulated && l3Full && postRestoreContinuitySqlProven(restoreExtended);

  const l5 = !simulated && l4 && restoredDbApplicationSmokeProven(restoreExtended);

  const latestDrillFailed = restoreLatest?.status === RestoreVerificationRunResponseDtoStatus.NUMBER_3;

  let highest: DrProofLevelIndex = 0;
  if (l5) highest = 5;
  else if (l4) highest = 4;
  else if (l3Full) highest = 3;
  else if (l2) highest = 2;
  else if (l1) highest = 1;
  else if (l0) highest = 0;

  if (simulated) {
    highest = Math.min(highest, 0) as DrProofLevelIndex;
  }

  const l6Partial =
    !simulated &&
    drillSucceeded(restoreLatest) &&
    restoreExtended.externalDependencyProofOutcome?.toLowerCase() === 'partial';

  const layers: DrProofLayerRow[] = [
    {
      id: 0,
      titleKey: 'backupDr.confidenceDashboard.layers.L0.title',
      detailKey: l0 ? 'backupDr.confidenceDashboard.layers.L0.detailOk' : 'backupDr.confidenceDashboard.layers.L0.detailGap',
      state: simulated ? 'stub_only' : l0 ? 'proven' : 'gap',
    },
    {
      id: 1,
      titleKey: 'backupDr.confidenceDashboard.layers.L1.title',
      detailKey: simulated
        ? 'backupDr.confidenceDashboard.layers.L1.detailStub'
        : l1
          ? 'backupDr.confidenceDashboard.layers.L1.detailOk'
          : 'backupDr.confidenceDashboard.layers.L1.detailGap',
      state: simulated ? 'stub_only' : l1 ? 'proven' : technicalOk ? 'partial' : 'gap',
    },
    {
      id: 2,
      titleKey: 'backupDr.confidenceDashboard.layers.L2.title',
      detailKey: simulated
        ? 'backupDr.confidenceDashboard.layers.L2.detailStub'
        : l2
          ? 'backupDr.confidenceDashboard.layers.L2.detailOk'
          : 'backupDr.confidenceDashboard.layers.L2.detailGap',
      state: simulated ? 'stub_only' : l2 ? 'proven' : l1 ? 'partial' : 'gap',
    },
    {
      id: 3,
      titleKey: 'backupDr.confidenceDashboard.layers.L3.title',
      detailKey: simulated
        ? 'backupDr.confidenceDashboard.layers.L3.detailStub'
        : l3Full
          ? 'backupDr.confidenceDashboard.layers.L3.detailOk'
          : l3Partial
            ? 'backupDr.confidenceDashboard.layers.L3.detailPartial'
            : latestDrillFailed
              ? 'backupDr.confidenceDashboard.layers.L3.detailFailed'
              : 'backupDr.confidenceDashboard.layers.L3.detailGap',
      state: simulated
        ? 'stub_only'
        : l3Full
          ? 'proven'
          : latestDrillFailed
            ? 'gap'
            : l3Partial
              ? 'partial'
              : 'gap',
    },
    {
      id: 4,
      titleKey: 'backupDr.confidenceDashboard.layers.L4.title',
      detailKey: simulated
        ? 'backupDr.confidenceDashboard.layers.L4.detailStub'
        : l4
          ? 'backupDr.confidenceDashboard.layers.L4.detailOk'
          : restoreExtended.postRestoreContinuityChecksExecuted === false
            ? 'backupDr.confidenceDashboard.layers.L4.detailNotConfigured'
            : 'backupDr.confidenceDashboard.layers.L4.detailGap',
      state: simulated ? 'stub_only' : l4 ? 'proven' : l3Full ? 'partial' : 'gap',
    },
    {
      id: 5,
      titleKey: 'backupDr.confidenceDashboard.layers.L5.title',
      detailKey: simulated
        ? 'backupDr.confidenceDashboard.layers.L5.detailStub'
        : l5
          ? 'backupDr.confidenceDashboard.layers.L5.detailOk'
          : l4 &&
              restoreExtended.restoredDatabaseApplicationSmokeExecuted === true &&
              restoreExtended.restoredDatabaseApplicationSmokePassed === false
            ? 'backupDr.confidenceDashboard.layers.L5.detailFailed'
            : 'backupDr.confidenceDashboard.layers.L5.detailGap',
      state: simulated
        ? 'stub_only'
        : l5
          ? 'proven'
          : l4 &&
              restoreExtended.restoredDatabaseApplicationSmokeExecuted === true &&
              restoreExtended.restoredDatabaseApplicationSmokePassed === false
            ? 'gap'
            : l4
              ? 'partial'
              : 'gap',
    },
    {
      id: 6,
      titleKey: 'backupDr.confidenceDashboard.layers.L6.title',
      detailKey: simulated
        ? 'backupDr.confidenceDashboard.layers.L6.detailStub'
        : l6Partial
          ? 'backupDr.confidenceDashboard.layers.L6.detailPartial'
          : 'backupDr.confidenceDashboard.layers.L6.detailGap',
      state: simulated ? 'stub_only' : l6Partial ? 'partial' : 'gap',
    },
  ];

  let nextStepKey = 'backupDr.confidenceDashboard.nextStepHints.finishL1';
  if (!l0) nextStepKey = 'backupDr.confidenceDashboard.nextStepHints.L0';
  else if (simulated) nextStepKey = 'backupDr.confidenceDashboard.nextStepHints.leaveStub';
  else if (!l1) nextStepKey = 'backupDr.confidenceDashboard.nextStepHints.L1';
  else if (!l2) nextStepKey = 'backupDr.confidenceDashboard.nextStepHints.L2';
  else if (!l3Full) nextStepKey = 'backupDr.confidenceDashboard.nextStepHints.L3';
  else if (!l4) nextStepKey = 'backupDr.confidenceDashboard.nextStepHints.L4';
  else if (!l5) nextStepKey = 'backupDr.confidenceDashboard.nextStepHints.L5';

  const suppressOptimisticTone = latestDrillFailed || simulated || truth.restore.latestDrillFailed;

  let alertType: 'success' | 'info' | 'warning' | 'error' = 'info';
  let titleKey = 'backupDr.confidenceDashboard.strip.inProgressTitle';
  let bodyKey = 'backupDr.confidenceDashboard.strip.inProgressBody';

  if (latestDrillFailed || truth.restore.latestDrillFailed) {
    alertType = 'error';
    titleKey = 'backupDr.confidenceDashboard.strip.drillFailedTitle';
    bodyKey = 'backupDr.confidenceDashboard.strip.drillFailedBody';
  } else if (simulated) {
    alertType = 'warning';
    titleKey = 'backupDr.confidenceDashboard.strip.stubTitle';
    bodyKey = 'backupDr.confidenceDashboard.strip.stubBody';
  } else if (l5) {
    alertType = 'success';
    titleKey = 'backupDr.confidenceDashboard.strip.l5Title';
    bodyKey = 'backupDr.confidenceDashboard.strip.l5Body';
  } else if (l4) {
    alertType = 'success';
    titleKey = 'backupDr.confidenceDashboard.strip.strongWithinApiTitle';
    bodyKey = 'backupDr.confidenceDashboard.strip.strongWithinApiBody';
  } else if (l3Full) {
    alertType = 'info';
    titleKey = 'backupDr.confidenceDashboard.strip.l3Title';
    bodyKey = 'backupDr.confidenceDashboard.strip.l3Body';
  } else if (truth.recoverability.hasProofGaps) {
    alertType = 'warning';
    titleKey = 'backupDr.confidenceDashboard.strip.gapsTitle';
    bodyKey = 'backupDr.confidenceDashboard.strip.gapsBody';
  }

  const lastGoodIsSim = recoverability?.lastSuccessfulBackupRunIsSimulatedExecution === true;
  const lastGoodId = recoverability?.lastSuccessfulBackupRunId?.trim();
  const latestReqId = latest?.id?.trim();
  const distinct = Boolean(lastGoodId && latestReqId && lastGoodId !== latestReqId);

  const realBackupArtifactSummary = {
    labelKey: lastGoodIsSim
      ? 'backupDr.confidenceDashboard.artifacts.lastGoodSimulated'
      : 'backupDr.confidenceDashboard.artifacts.lastGoodReal',
    runId: lastGoodId,
    isDistinctFromLatestRequest: distinct,
  };

  const drillOk = drillSucceeded(restoreLatest);
  const restoreVerifiedSummary = {
    labelKey: drillOk
      ? 'backupDr.confidenceDashboard.restore.verifiedOk'
      : 'backupDr.confidenceDashboard.restore.notVerified',
    sourceBackupRunId: restoreLatest?.sourceBackupRunId ?? undefined,
    sourceArtifactId: restoreExtended.sourceBackupArtifactId ?? undefined,
    drillSucceeded: drillOk,
  };

  return {
    highestFullyProvenLevel: highest,
    layers,
    nextStepKey,
    decisionStrip: {
      alertType,
      titleKey,
      bodyKey,
      suppressOptimisticTone,
    },
    latestRealBackupArtifactSummary: realBackupArtifactSummary,
    latestRestoreVerifiedSummary: restoreVerifiedSummary,
    fiscalVerifiedSummary: {
      labelKey:
        fiscalContinuityProven(restoreLatest, restoreExtended)
          ? 'backupDr.confidenceDashboard.fiscal.proven'
          : 'backupDr.confidenceDashboard.fiscal.notProven',
      passed: restoreLatest?.fiscalSqlPassed ?? undefined,
      skipped: restoreLatest?.fiscalSqlSkipped,
    },
    appRecoverySummary: (() => {
      const l5aOk = restoredDbApplicationSmokeProven(restoreExtended);
      const l5bOk = applicationSmokeProven(restoreLatest, restoreExtended);
      const anyOk = l5aOk || l5bOk;
      const l5aFailed =
        restoreExtended.restoredDatabaseApplicationSmokeExecuted === true &&
        restoreExtended.restoredDatabaseApplicationSmokePassed === false;
      const l5bFailed =
        restoreExtended.applicationSmokeProbeExecuted === true &&
        restoreExtended.applicationSmokeProbePassed === false;
      const anyFailed = l5aFailed || l5bFailed;
      const nothingExecuted =
        restoreExtended.restoredDatabaseApplicationSmokeExecuted !== true &&
        restoreExtended.applicationSmokeProbeExecuted !== true;
      return {
        labelKey: anyOk
          ? 'backupDr.confidenceDashboard.appRecovery.proven'
          : 'backupDr.confidenceDashboard.appRecovery.notProven',
        state: anyOk
          ? ('proven' as const)
          : anyFailed
            ? ('failed' as const)
            : nothingExecuted
              ? ('not_configured' as const)
              : ('not_proven' as const),
      };
    })(),
    externalDepsSummary: {
      labelKey: l6Partial
        ? 'backupDr.confidenceDashboard.external.partialConfigSnapshot'
        : 'backupDr.confidenceDashboard.external.notProven',
      state: l6Partial ? 'partial_configuration' : 'not_proven',
    },
  };
}

/** Hızlı tarama şeridi — yeşil “healthy” yok; kırmızı/turuncu/mavi işlemsel tonlar. */
export type DrProofScanTagTone = 'error' | 'warning' | 'processing' | 'default';

export interface DrProofScanTag {
  labelKey: string;
  tone: DrProofScanTagTone;
}

/** Ant Design `Tag` preset `color` — tutarlı uyarı / işlem tonları (yeşil yok). */
export function mapDrProofScanTagToneToAntdTagColor(
  tone: DrProofScanTagTone,
): 'red' | 'orange' | 'blue' | 'default' {
  if (tone === 'error') return 'red';
  if (tone === 'warning') return 'orange';
  if (tone === 'processing') return 'blue';
  return 'default';
}

/**
 * Üst karar şeridinde 2–5 sn tarama: tatbikat, kanıt boşlukları, API kapsamı (tam kurum kurtarması değil).
 */
export function buildDrProofScanTags(params: {
  model: DrProofPresentationModel;
  restoreLatest: RestoreVerificationRunResponseDto | undefined;
  truth: BackupOperatorTruthModel;
}): DrProofScanTag[] {
  const { model, restoreLatest, truth } = params;
  const tags: DrProofScanTag[] = [];

  const st = restoreLatest?.status;
  const drillFailed =
    st === RestoreVerificationRunResponseDtoStatus.NUMBER_3 || truth.restore.latestDrillFailed === true;
  const drillSucceeded = st === RestoreVerificationRunResponseDtoStatus.NUMBER_2;
  const drillRunning =
    st === RestoreVerificationRunResponseDtoStatus.NUMBER_0 ||
    st === RestoreVerificationRunResponseDtoStatus.NUMBER_1;

  if (drillFailed) {
    tags.push({ labelKey: 'backupDr.scan.drill.latestFailed', tone: 'error' });
  } else if (drillRunning) {
    tags.push({ labelKey: 'backupDr.scan.drill.inProgress', tone: 'warning' });
  } else if (drillSucceeded) {
    tags.push({ labelKey: 'backupDr.scan.drill.latestSucceededApi', tone: 'processing' });
  } else if (restoreLatest != null) {
    tags.push({ labelKey: 'backupDr.scan.drill.noTerminalOutcome', tone: 'default' });
  }

  if (truth.run.simulatedEvidence) {
    tags.push({ labelKey: 'backupDr.scan.pipeline.stubOrSimulated', tone: 'warning' });
  }

  if (truth.recoverability.hasProofGaps) {
    tags.push({ labelKey: 'backupDr.scan.proofTimestampsIncomplete', tone: 'warning' });
  }

  const h = model.highestFullyProvenLevel;
  if (!drillFailed && !truth.run.simulatedEvidence) {
    if (h < 3) {
      tags.push({ labelKey: 'backupDr.scan.scope.endToEndRestoreNotProven', tone: 'warning' });
    } else if (h >= 3 && h < 5) {
      tags.push({ labelKey: 'backupDr.scan.scope.apiDbCentricNotFullSystem', tone: 'processing' });
    } else if (h >= 5) {
      tags.push({ labelKey: 'backupDr.scan.scope.apiIncludesAppLayerNotFullOrg', tone: 'processing' });
    }
  }

  return tags;
}
