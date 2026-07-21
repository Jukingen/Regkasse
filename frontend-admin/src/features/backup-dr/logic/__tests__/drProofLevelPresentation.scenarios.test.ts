/**
 * DR kanıt matrisi: operatör-visible gerçekler — snapshot değil anlamsal iddialar.
 */
import { describe, expect, it } from 'vitest';

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
import type { BackupOperatorTruthModel } from '@/features/backup-dr/logic/backupDrOperatorTruthModel';
import {
  buildDrProofPresentationModel,
  buildDrProofScanTags,
} from '@/features/backup-dr/logic/drProofLevelPresentation';

function truth(p: {
  simulated?: boolean;
  realPg?: boolean;
  hasProofGaps?: boolean;
  latestDrillFailed?: boolean;
}): BackupOperatorTruthModel {
  return {
    run: {
      simulatedEvidence: p.simulated ?? false,
      realPostgreSqlLogicalDumpConfigured: p.realPg ?? true,
    },
    recoverability: { hasProofGaps: p.hasProofGaps ?? false },
    restore: { latestDrillFailed: p.latestDrillFailed ?? false },
  } as unknown as BackupOperatorTruthModel;
}

function pgDumpLatest(id: string): BackupRunResponseDto {
  return {
    id,
    status: BackupRunResponseDtoStatus.NUMBER_3,
    artifacts: [
      {
        artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
        isFilePresentForDownload: true,
      },
    ],
  };
}

describe('buildDrProofPresentationModel — contradictory / edge scenarios', () => {
  it('real artifact + verification OK but no successful restore drill: does not claim L3 full', () => {
    const latest = pgDumpLatest('run-new');
    const m = buildDrProofPresentationModel({
      truth: truth({ simulated: false, realPg: true }),
      latest,
      detailForPipeline: latest,
      verification: { status: 1, backupRunId: 'run-new' } as BackupVerificationResponseDto,
      recoverability: {
        lastSuccessfulBackupAt: '2026-01-01T00:00:00Z',
      } as BackupRecoverabilitySummaryResponseDto,
      restoreLatest: {
        status: RestoreVerificationRunResponseDtoStatus.NUMBER_1,
      } as RestoreVerificationRunResponseDto,
      restoreExtended: {},
    });
    expect(m.highestFullyProvenLevel).toBe(2);
    expect(m.layers[3].state).not.toBe('proven');
  });

  it('restore drill succeeded but dump list not OK: L3 partial, not full', () => {
    const latest = pgDumpLatest('r1');
    const restore: RestoreVerificationRunResponseDto = {
      status: RestoreVerificationRunResponseDtoStatus.NUMBER_2,
      dumpInspectionPassed: false,
      pgRestoreListExitCode: 2,
    };
    const m = buildDrProofPresentationModel({
      truth: truth({ simulated: false, realPg: true }),
      latest,
      detailForPipeline: latest,
      verification: { status: 1, backupRunId: 'r1' } as BackupVerificationResponseDto,
      recoverability: {} as BackupRecoverabilitySummaryResponseDto,
      restoreLatest: restore,
      restoreExtended: {},
    });
    expect(m.layers[3].state).toBe('partial');
    expect(m.highestFullyProvenLevel).toBe(2);
  });

  it('restore + isolated pass + post-restore SQL pass: L4 even if fiscal SQL failed', () => {
    const latest = pgDumpLatest('r1');
    const restore: RestoreVerificationRunResponseDto = {
      status: RestoreVerificationRunResponseDtoStatus.NUMBER_2,
      dumpInspectionPassed: true,
      restoreAttemptExecuted: true,
      restoreAttemptPassed: true,
      fiscalSqlSkipped: false,
      fiscalSqlPassed: false,
    };
    const m = buildDrProofPresentationModel({
      truth: truth({ simulated: false, realPg: true }),
      latest,
      detailForPipeline: latest,
      verification: { status: 1, backupRunId: 'r1' } as BackupVerificationResponseDto,
      recoverability: {} as BackupRecoverabilitySummaryResponseDto,
      restoreLatest: restore,
      restoreExtended: {
        postRestoreContinuityChecksExecuted: true,
        postRestoreContinuityChecksPassed: true,
      },
    });
    expect(m.highestFullyProvenLevel).toBe(4);
    expect(m.layers[4].state).toBe('proven');
    expect(m.fiscalVerifiedSummary.passed).toBe(false);
  });

  it('L4 post-restore SQL pass: L5 partial (restored-DB smoke not configured), L6 gap without external evidence', () => {
    const latest = pgDumpLatest('r1');
    const restore: RestoreVerificationRunResponseDto = {
      status: RestoreVerificationRunResponseDtoStatus.NUMBER_2,
      dumpInspectionPassed: true,
      restoreAttemptExecuted: true,
      restoreAttemptPassed: true,
      fiscalSqlSkipped: false,
      fiscalSqlPassed: true,
    };
    const m = buildDrProofPresentationModel({
      truth: truth({ simulated: false, realPg: true }),
      latest,
      detailForPipeline: latest,
      verification: { status: 1, backupRunId: 'r1' } as BackupVerificationResponseDto,
      recoverability: {} as BackupRecoverabilitySummaryResponseDto,
      restoreLatest: restore,
      restoreExtended: {
        postRestoreContinuityChecksExecuted: true,
        postRestoreContinuityChecksPassed: true,
        fiscalContinuityLayerPassed: true,
      },
    });
    expect(m.highestFullyProvenLevel).toBe(4);
    expect(m.layers[5].state).toBe('partial');
    expect(m.layers[6].state).toBe('gap');
  });

  it('latest backup request id differs from last-good recoverability run: surfaces distinct proof', () => {
    const m = buildDrProofPresentationModel({
      truth: truth({ simulated: false, realPg: true }),
      latest: pgDumpLatest('latest-request'),
      detailForPipeline: pgDumpLatest('latest-request'),
      verification: { status: 1, backupRunId: 'latest-request' } as BackupVerificationResponseDto,
      recoverability: {
        lastSuccessfulBackupAt: '2026-01-01T00:00:00Z',
        lastSuccessfulBackupRunId: 'older-good',
      } as BackupRecoverabilitySummaryResponseDto,
      restoreLatest: {
        status: RestoreVerificationRunResponseDtoStatus.NUMBER_2,
      } as RestoreVerificationRunResponseDto,
      restoreExtended: {},
    });
    expect(m.latestRealBackupArtifactSummary.isDistinctFromLatestRequest).toBe(true);
    expect(m.latestRealBackupArtifactSummary.runId).toBe('older-good');
  });

  it('simulated truth caps proof even if older API would show success drill', () => {
    const latest = pgDumpLatest('x');
    const m = buildDrProofPresentationModel({
      truth: truth({ simulated: true, realPg: true }),
      latest,
      detailForPipeline: latest,
      verification: { status: 1, backupRunId: 'x' } as BackupVerificationResponseDto,
      recoverability: {
        lastSuccessfulBackupAt: '2026-01-01T00:00:00Z',
      } as BackupRecoverabilitySummaryResponseDto,
      restoreLatest: {
        status: RestoreVerificationRunResponseDtoStatus.NUMBER_2,
      } as RestoreVerificationRunResponseDto,
      restoreExtended: {},
    });
    expect(m.highestFullyProvenLevel).toBe(0);
  });

  it('missing DTO fields: empty extended + undefined restore does not imply success', () => {
    const m = buildDrProofPresentationModel({
      truth: truth({}),
      latest: undefined,
      detailForPipeline: undefined,
      verification: undefined,
      recoverability: undefined,
      restoreLatest: undefined,
      restoreExtended: {},
    });
    expect(m.decisionStrip.alertType).not.toBe('success');
    expect(m.latestRestoreVerifiedSummary.drillSucceeded).toBe(false);
  });

  it('strongWithinApi success strip does not use fake healthy wording', () => {
    const latest = pgDumpLatest('r1');
    const restore: RestoreVerificationRunResponseDto = {
      status: RestoreVerificationRunResponseDtoStatus.NUMBER_2,
      dumpInspectionPassed: true,
      restoreAttemptExecuted: true,
      restoreAttemptPassed: true,
      fiscalSqlSkipped: false,
      fiscalSqlPassed: true,
    };
    const m = buildDrProofPresentationModel({
      truth: truth({ simulated: false, realPg: true, hasProofGaps: false }),
      latest,
      detailForPipeline: latest,
      verification: { status: 1, backupRunId: 'r1' } as BackupVerificationResponseDto,
      recoverability: {} as BackupRecoverabilitySummaryResponseDto,
      restoreLatest: restore,
      restoreExtended: {
        postRestoreContinuityChecksExecuted: true,
        postRestoreContinuityChecksPassed: true,
      },
    });
    expect(m.decisionStrip.alertType).toBe('success');
    expect(m.decisionStrip.titleKey).toBe(
      'backupDr.confidenceDashboard.strip.strongWithinApiTitle'
    );
    expect(m.decisionStrip.titleKey).not.toContain('health');
  });

  it('proof gaps: warning strip when recoverability has gaps', () => {
    const m = buildDrProofPresentationModel({
      truth: truth({ simulated: false, realPg: true, hasProofGaps: true }),
      latest: pgDumpLatest('a'),
      detailForPipeline: pgDumpLatest('a'),
      verification: undefined,
      recoverability: {} as BackupRecoverabilitySummaryResponseDto,
      restoreLatest: undefined,
      restoreExtended: {},
    });
    expect(m.decisionStrip.alertType).toBe('warning');
    expect(m.decisionStrip.titleKey).toBe('backupDr.confidenceDashboard.strip.gapsTitle');
  });

  it('next step key points to a concrete hint path', () => {
    const m = buildDrProofPresentationModel({
      truth: truth({ simulated: false, realPg: true }),
      latest: pgDumpLatest('a'),
      detailForPipeline: pgDumpLatest('a'),
      verification: undefined,
      recoverability: {} as BackupRecoverabilitySummaryResponseDto,
      restoreLatest: undefined,
      restoreExtended: {},
    });
    expect(m.nextStepKey.startsWith('backupDr.confidenceDashboard.nextStepHints.')).toBe(true);
  });
});

describe('buildDrProofScanTags — operator scan ribbon', () => {
  it('failed latest drill: error tone and dominant over scope hints', () => {
    const tr = truth({ simulated: false, realPg: true, hasProofGaps: false });
    const restoreFailed = {
      status: RestoreVerificationRunResponseDtoStatus.NUMBER_3,
    } as RestoreVerificationRunResponseDto;
    const m = buildDrProofPresentationModel({
      truth: tr,
      latest: pgDumpLatest('x'),
      detailForPipeline: pgDumpLatest('x'),
      verification: { status: 1, backupRunId: 'x' } as BackupVerificationResponseDto,
      recoverability: {} as BackupRecoverabilitySummaryResponseDto,
      restoreLatest: restoreFailed,
      restoreExtended: {},
    });
    const tags = buildDrProofScanTags({
      model: m,
      restoreLatest: restoreFailed,
      truth: tr,
    });
    expect(tags.find((x) => x.labelKey === 'backupDr.scan.drill.latestFailed')?.tone).toBe('error');
    expect(tags.some((x) => x.labelKey === 'backupDr.scan.scope.apiDbCentricNotFullSystem')).toBe(
      false
    );
  });

  it('simulated pipeline: warning tag present', () => {
    const tr = truth({ simulated: true, realPg: true });
    const m = buildDrProofPresentationModel({
      truth: tr,
      latest: undefined,
      detailForPipeline: undefined,
      verification: undefined,
      recoverability: undefined,
      restoreLatest: undefined,
      restoreExtended: {},
    });
    const tags = buildDrProofScanTags({
      model: m,
      restoreLatest: undefined,
      truth: tr,
    });
    expect(tags.some((x) => x.labelKey === 'backupDr.scan.pipeline.stubOrSimulated')).toBe(true);
  });
});
