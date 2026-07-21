/**
 * Yedek kanıt merdiveni: teknik başarı ile gerçek pg_dump / liste tatbikatı / kurtarma kanıtını ayırır (DTO sınırları içinde).
 * Başlık önceliği (`pickHeadline`) ve kanıt gücü özeti: `truthProvenance` ile hizalanacak şekilde `backupDrTruthExtensionPoints.ts` üzerinden dokümante edilir.
 */
import type {
  BackupArtifactResponseDto,
  BackupRecoverabilitySummaryResponseDto,
  BackupRunResponseDto,
  BackupVerificationResponseDto,
  RestoreVerificationRunResponseDto,
} from '@/api/generated/model';
import { BackupArtifactResponseDtoArtifactType } from '@/api/generated/model/backupArtifactResponseDtoArtifactType';
import { BackupRunResponseDtoStatus } from '@/api/generated/model/backupRunResponseDtoStatus';
import { BackupVerificationResponseDtoStatus } from '@/api/generated/model/backupVerificationResponseDtoStatus';
import { RestoreVerificationRunResponseDtoStatus } from '@/api/generated/model/restoreVerificationRunResponseDtoStatus';
import type { BackupExecutionModeTruth } from '@/features/backup-dr/logic/backupDrExecutionModeTruth';
import { unloadedBackupExecutionModeTruth } from '@/features/backup-dr/logic/backupDrExecutionModeTruth';
import { mapDumpInspectionTriState } from '@/features/backup-dr/logic/backupDrMappers';

function hasRecoverabilityProofGaps(
  summary: BackupRecoverabilitySummaryResponseDto | undefined
): boolean {
  if (!summary) return true;
  const noBackupProof = !summary.lastSuccessfulBackupAt;
  const noArtifactProof = !summary.lastSuccessfulArtifactVerificationAt;
  const noRestoreProof = !summary.lastSuccessfulRestoreProofAt;
  return noBackupProof || noArtifactProof || noRestoreProof;
}

/** Satır etiketi — AntD Tag rengi ile eşlenir. */
export type EvidenceStepStatus = 'pass' | 'fail' | 'unknown' | 'na' | 'limited';

export interface EvidenceStepRow {
  id: string;
  labelKey: string;
  detailKey: string;
  status: EvidenceStepStatus;
}

export type EvidenceHeadlineTone = 'success' | 'info' | 'warning' | 'neutral';

export interface BackupEvidenceLadderModel {
  steps: EvidenceStepRow[];
  headlineKey: string;
  headlineTone: EvidenceHeadlineTone;
  /** API’nin henüz sunmadığı operatör kanıtları (PGDMP baytı vb.). */
  backendSignalGaps: string[];
}

function logicalDumpPresence(
  artifacts: BackupArtifactResponseDto[] | undefined | null,
  hasLogicalDumpArtifactFlag: boolean | undefined
): EvidenceStepStatus {
  const list = artifacts ?? [];
  const row = list.find((a) => a.artifactType === BackupArtifactResponseDtoArtifactType.NUMBER_0);
  if (row) {
    if (row.isFilePresentForDownload === true) return 'pass';
    if (row.isFilePresentForDownload === false) return 'fail';
    return 'unknown';
  }
  if (hasLogicalDumpArtifactFlag === true) return 'unknown';
  if (hasLogicalDumpArtifactFlag === false) return 'fail';
  return 'unknown';
}

function artifactVerificationStep(
  verification: BackupVerificationResponseDto | undefined,
  latestRunId: string | undefined
): EvidenceStepStatus {
  if (!verification || verification.status === undefined || verification.status === null)
    return 'unknown';
  if (verification.status === BackupVerificationResponseDtoStatus.NUMBER_0) return 'unknown';
  const bid = verification.backupRunId?.trim();
  if (latestRunId && bid && bid !== latestRunId) return 'unknown';
  if (verification.status === BackupVerificationResponseDtoStatus.NUMBER_1) return 'pass';
  if (verification.status === BackupVerificationResponseDtoStatus.NUMBER_2) return 'fail';
  return 'unknown';
}

/**
 * Kanıt merdivenini üretir — `buildBackupOperatorTruthModel` içinden beslenir.
 */
export function deriveBackupEvidenceLadder(params: {
  latest: BackupRunResponseDto | undefined;
  detailForPipeline: BackupRunResponseDto | null | undefined;
  verification: BackupVerificationResponseDto | undefined;
  restoreLatest: RestoreVerificationRunResponseDto | undefined;
  recoverabilitySummary: BackupRecoverabilitySummaryResponseDto | undefined;
  /** `deriveRunTruth` çıktısı */
  simulatedEvidence: boolean;
  realPostgreSqlLogicalDumpConfigured: boolean | undefined;
  /** Admin execution-mode özeti; başlıkta requested/effective/blokaj netliği. */
  executionMode?: BackupExecutionModeTruth;
}): BackupEvidenceLadderModel {
  const {
    latest,
    detailForPipeline,
    verification,
    restoreLatest,
    recoverabilitySummary,
    simulatedEvidence,
    realPostgreSqlLogicalDumpConfigured,
    executionMode = unloadedBackupExecutionModeTruth,
  } = params;

  const detail = detailForPipeline ?? null;
  const arts = detail?.artifacts ?? latest?.artifacts ?? undefined;
  const latestId = latest?.id?.trim();
  const technicalOk = latest?.status === BackupRunResponseDtoStatus.NUMBER_3;

  const stepTechnical: EvidenceStepRow = {
    id: 'technical_job',
    labelKey: 'backupDr.evidence.steps.technicalJob.label',
    detailKey: 'backupDr.evidence.steps.technicalJob.detail',
    status: !latest ? 'unknown' : technicalOk ? 'pass' : 'fail',
  };

  const stepRealPgConfigured: EvidenceStepRow = {
    id: 'real_pg_config',
    labelKey: 'backupDr.evidence.steps.realPgConfigured.label',
    detailKey: 'backupDr.evidence.steps.realPgConfigured.detail',
    status:
      realPostgreSqlLogicalDumpConfigured === true
        ? 'pass'
        : realPostgreSqlLogicalDumpConfigured === false
          ? 'fail'
          : 'unknown',
  };

  const stepNonStub: EvidenceStepRow = {
    id: 'non_stub_run',
    labelKey: 'backupDr.evidence.steps.nonStubRun.label',
    detailKey: 'backupDr.evidence.steps.nonStubRun.detail',
    status: !technicalOk ? 'na' : simulatedEvidence ? 'fail' : 'pass',
  };

  const logicalPresence = logicalDumpPresence(
    arts,
    detail?.hasLogicalDumpArtifact ?? latest?.hasLogicalDumpArtifact
  );
  const logicalDetailKey = (() => {
    if (!technicalOk) return 'backupDr.evidence.steps.logicalDumpFile.detailNa';
    if (simulatedEvidence) return 'backupDr.evidence.steps.logicalDumpFile.detailStub';
    if (logicalPresence === 'pass') return 'backupDr.evidence.steps.logicalDumpFile.detailPass';
    if (logicalPresence === 'fail') return 'backupDr.evidence.steps.logicalDumpFile.detailFail';
    return 'backupDr.evidence.steps.logicalDumpFile.detailUnknown';
  })();
  const stepLogicalFile: EvidenceStepRow = {
    id: 'logical_dump_row',
    labelKey: 'backupDr.evidence.steps.logicalDumpFile.label',
    detailKey: logicalDetailKey,
    status: !technicalOk ? 'na' : simulatedEvidence ? 'limited' : logicalPresence,
  };

  const ver = artifactVerificationStep(verification, latestId);
  const verificationDetailKey = (() => {
    if (!technicalOk) return 'backupDr.evidence.steps.artifactVerification.detailNa';
    if (simulatedEvidence) return 'backupDr.evidence.steps.artifactVerification.detailStub';
    if (ver === 'pass') return 'backupDr.evidence.steps.artifactVerification.detailPass';
    if (ver === 'fail') return 'backupDr.evidence.steps.artifactVerification.detailFail';
    return 'backupDr.evidence.steps.artifactVerification.detailUnknown';
  })();
  const stepVerification: EvidenceStepRow = {
    id: 'artifact_verification',
    labelKey: 'backupDr.evidence.steps.artifactVerification.label',
    detailKey: verificationDetailKey,
    status: !technicalOk ? 'na' : simulatedEvidence ? 'limited' : ver,
  };

  let listStatus: EvidenceStepStatus = 'unknown';
  let listDetailKey = 'backupDr.evidence.steps.dumpListInspection.detailNoDrill';
  const di = restoreLatest ? mapDumpInspectionTriState(restoreLatest) : undefined;
  const rvSt = restoreLatest?.status;

  if (!restoreLatest) {
    listDetailKey = 'backupDr.evidence.steps.dumpListInspection.detailNoDrill';
  } else if (
    rvSt === RestoreVerificationRunResponseDtoStatus.NUMBER_0 ||
    rvSt === RestoreVerificationRunResponseDtoStatus.NUMBER_1
  ) {
    listStatus = 'unknown';
    listDetailKey = 'backupDr.evidence.steps.dumpListInspection.detailDrillInFlight';
  } else if (rvSt === RestoreVerificationRunResponseDtoStatus.NUMBER_3) {
    if (simulatedEvidence && technicalOk) {
      listStatus = 'limited';
      listDetailKey = 'backupDr.evidence.steps.dumpListInspection.detailStubExpected';
    } else if (di === false) {
      listStatus = 'fail';
      listDetailKey = 'backupDr.evidence.steps.dumpListInspection.detailFailReal';
    } else if (di === true) {
      listStatus = 'pass';
      listDetailKey = 'backupDr.evidence.steps.dumpListInspection.detailPassIndirect';
    } else {
      listStatus = 'unknown';
      listDetailKey = 'backupDr.evidence.steps.dumpListInspection.detailUnknown';
    }
  } else if (rvSt === RestoreVerificationRunResponseDtoStatus.NUMBER_2) {
    if (di === true) {
      listStatus = 'pass';
      listDetailKey = 'backupDr.evidence.steps.dumpListInspection.detailPassIndirect';
    } else if (di === false) {
      listStatus = simulatedEvidence ? 'limited' : 'fail';
      listDetailKey = simulatedEvidence
        ? 'backupDr.evidence.steps.dumpListInspection.detailStubExpected'
        : 'backupDr.evidence.steps.dumpListInspection.detailFailReal';
    } else {
      listStatus = 'unknown';
      listDetailKey = 'backupDr.evidence.steps.dumpListInspection.detailUnknown';
    }
  } else {
    listStatus = 'unknown';
    listDetailKey = 'backupDr.evidence.steps.dumpListInspection.detailUnknown';
  }

  const stepDumpList: EvidenceStepRow = {
    id: 'dump_list',
    labelKey: 'backupDr.evidence.steps.dumpListInspection.label',
    detailKey: listDetailKey,
    status: listStatus,
  };

  const drillOk = restoreLatest?.status === RestoreVerificationRunResponseDtoStatus.NUMBER_2;
  const drillQueuedOrRunning =
    restoreLatest?.status === RestoreVerificationRunResponseDtoStatus.NUMBER_0 ||
    restoreLatest?.status === RestoreVerificationRunResponseDtoStatus.NUMBER_1;
  const stepDrill: EvidenceStepRow = {
    id: 'restore_drill_ok',
    labelKey: 'backupDr.evidence.steps.restoreDrillCompleted.label',
    detailKey: !restoreLatest
      ? 'backupDr.evidence.steps.restoreDrillCompleted.detailNoRow'
      : drillQueuedOrRunning
        ? 'backupDr.evidence.steps.restoreDrillCompleted.detailInFlight'
        : drillOk
          ? 'backupDr.evidence.steps.restoreDrillCompleted.detailPass'
          : 'backupDr.evidence.steps.restoreDrillCompleted.detailNotOk',
    status: !restoreLatest
      ? 'unknown'
      : drillQueuedOrRunning
        ? 'unknown'
        : drillOk
          ? 'pass'
          : 'fail',
  };

  const proofGap = hasRecoverabilityProofGaps(recoverabilitySummary);
  const stepProofTimestamps: EvidenceStepRow = {
    id: 'recoverability_proof',
    labelKey: 'backupDr.evidence.steps.recoverabilityProof.label',
    detailKey: proofGap
      ? 'backupDr.evidence.steps.recoverabilityProof.detailGap'
      : 'backupDr.evidence.steps.recoverabilityProof.detailOk',
    status: proofGap ? 'fail' : 'pass',
  };

  const isolatedOk =
    restoreLatest?.restoreAttemptExecuted === true && restoreLatest.restoreAttemptPassed === true;
  const stepIsolated: EvidenceStepRow = {
    id: 'isolated_restore',
    labelKey: 'backupDr.evidence.steps.isolatedRestore.label',
    detailKey:
      restoreLatest?.restoreAttemptExecuted !== true
        ? 'backupDr.evidence.steps.isolatedRestore.detailNotRun'
        : isolatedOk
          ? 'backupDr.evidence.steps.isolatedRestore.detailPass'
          : 'backupDr.evidence.steps.isolatedRestore.detailFail',
    status:
      restoreLatest?.restoreAttemptExecuted !== true
        ? 'na'
        : isolatedOk
          ? 'pass'
          : restoreLatest.restoreAttemptPassed === false
            ? 'fail'
            : 'unknown',
  };

  const steps: EvidenceStepRow[] = [
    stepTechnical,
    stepRealPgConfigured,
    stepNonStub,
    stepLogicalFile,
    stepVerification,
    stepDumpList,
    stepDrill,
    stepProofTimestamps,
    stepIsolated,
  ];

  const backendSignalGaps: string[] = [
    'backupDr.evidence.gaps.pgdmpHeader',
    'backupDr.evidence.gaps.stagingPathRedacted',
  ];

  const headline = pickHeadline({
    technicalOk,
    simulatedEvidence,
    realPostgreSqlLogicalDumpConfigured,
    logicalPresence,
    listStatus,
    drillOk,
    latestDrillFailed: restoreLatest?.status === RestoreVerificationRunResponseDtoStatus.NUMBER_3,
    proofGap,
    requestedRealButBlocked: executionMode.loaded && executionMode.requestedRealButBlocked,
    requestedRealButEffectiveSimulated:
      executionMode.loaded && executionMode.requestedRealButEffectiveSimulated,
  });

  return {
    steps,
    headlineKey: headline.key,
    headlineTone: headline.tone,
    backendSignalGaps,
  };
}

function pickHeadline(p: {
  technicalOk: boolean;
  simulatedEvidence: boolean;
  realPostgreSqlLogicalDumpConfigured: boolean | undefined;
  logicalPresence: EvidenceStepStatus;
  listStatus: EvidenceStepStatus;
  drillOk: boolean;
  latestDrillFailed: boolean;
  proofGap: boolean;
  requestedRealButBlocked: boolean;
  requestedRealButEffectiveSimulated: boolean;
}): { key: string; tone: EvidenceHeadlineTone } {
  if (!p.technicalOk) {
    return { key: 'backupDr.evidence.headline.noTechnicalSuccess', tone: 'warning' };
  }
  if (p.requestedRealButEffectiveSimulated) {
    return { key: 'backupDr.evidence.headline.requestedRealButEffectiveStub', tone: 'warning' };
  }
  if (p.requestedRealButBlocked && !p.simulatedEvidence) {
    return {
      key: 'backupDr.evidence.headline.requestedRealButPrerequisitesBlocked',
      tone: 'warning',
    };
  }
  if (p.simulatedEvidence) {
    return { key: 'backupDr.evidence.headline.stubPipeline', tone: 'info' };
  }
  if (p.realPostgreSqlLogicalDumpConfigured !== true) {
    return { key: 'backupDr.evidence.headline.noRealPgPath', tone: 'warning' };
  }
  if (p.logicalPresence !== 'pass') {
    return { key: 'backupDr.evidence.headline.realPathButFileUncertain', tone: 'warning' };
  }
  if (p.latestDrillFailed) {
    return { key: 'backupDr.evidence.headline.latestDrillFailed', tone: 'warning' };
  }
  if (p.listStatus === 'pass' && p.drillOk && !p.proofGap) {
    return { key: 'backupDr.evidence.headline.strongWithinApi', tone: 'success' };
  }
  if (p.listStatus === 'pass') {
    return { key: 'backupDr.evidence.headline.listOkMoreProofMayBeMissing', tone: 'info' };
  }
  return { key: 'backupDr.evidence.headline.realDumpWorkInProgress', tone: 'info' };
}
