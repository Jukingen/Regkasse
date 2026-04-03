import { describe, expect, it } from 'vitest';
import type { BackupOperatorTruthModel } from '@/features/backup-dr/logic/backupDrOperatorTruthModel';
import {
  BackupArtifactResponseDtoArtifactType,
  BackupRunResponseDtoStatus,
  RestoreVerificationRunResponseDtoStatus,
} from '@/api/generated/model';
import type {
  BackupRecoverabilitySummaryResponseDto,
  BackupRunResponseDto,
  BackupVerificationResponseDto,
  RestoreVerificationRunResponseDto,
} from '@/api/generated/model';
import {
  buildDrProofPresentationModel,
  mapDrProofScanTagToneToAntdTagColor,
} from '@/features/backup-dr/logic/drProofLevelPresentation';

function mkTruth(partial: {
  simulated?: boolean;
  realPg?: boolean;
  hasProofGaps?: boolean;
  latestDrillFailed?: boolean;
}): BackupOperatorTruthModel {
  return {
    run: {
      simulatedEvidence: partial.simulated ?? false,
      realPostgreSqlLogicalDumpConfigured: partial.realPg ?? true,
    },
    recoverability: {
      hasProofGaps: partial.hasProofGaps ?? false,
    },
    restore: {
      latestDrillFailed: partial.latestDrillFailed ?? false,
    },
  } as unknown as BackupOperatorTruthModel;
}

describe('buildDrProofPresentationModel', () => {
  it('stub/simulated pipeline does not yield high proof layers', () => {
    const latest: BackupRunResponseDto = {
      id: 'run-a',
      status: BackupRunResponseDtoStatus.NUMBER_3,
      artifacts: [
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
          isFilePresentForDownload: true,
        },
      ],
    };
    const m = buildDrProofPresentationModel({
      truth: mkTruth({ simulated: true, realPg: true }),
      latest,
      detailForPipeline: latest,
      verification: { status: 1, backupRunId: 'run-a' } as BackupVerificationResponseDto,
      recoverability: {
        lastSuccessfulBackupAt: '2026-01-01T00:00:00Z',
      } as BackupRecoverabilitySummaryResponseDto,
      restoreLatest: { status: RestoreVerificationRunResponseDtoStatus.NUMBER_2 } as RestoreVerificationRunResponseDto,
      restoreExtended: {},
    });
    expect(m.highestFullyProvenLevel).toBe(0);
    expect(m.layers[1].state).toBe('stub_only');
    expect(m.decisionStrip.alertType).toBe('warning');
  });

  it('failed latest restore drill forces error strip and suppresses optimistic tone', () => {
    const m = buildDrProofPresentationModel({
      truth: mkTruth({ latestDrillFailed: true }),
      latest: undefined,
      detailForPipeline: undefined,
      verification: undefined,
      recoverability: {},
      restoreLatest: { status: RestoreVerificationRunResponseDtoStatus.NUMBER_3 } as RestoreVerificationRunResponseDto,
      restoreExtended: {},
    });
    expect(m.decisionStrip.alertType).toBe('error');
    expect(m.decisionStrip.suppressOptimisticTone).toBe(true);
  });

  it('reports L4 when restore + post-restore continuity SQL pass (API signals)', () => {
    const rid = 'run-prod';
    const latest: BackupRunResponseDto = {
      id: rid,
      status: BackupRunResponseDtoStatus.NUMBER_3,
      artifacts: [
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
          isFilePresentForDownload: true,
        },
      ],
    };
    const restore: RestoreVerificationRunResponseDto = {
      status: RestoreVerificationRunResponseDtoStatus.NUMBER_2,
      dumpInspectionPassed: true,
      restoreAttemptExecuted: true,
      restoreAttemptPassed: true,
      fiscalSqlSkipped: false,
      fiscalSqlPassed: true,
    };
    const m = buildDrProofPresentationModel({
      truth: mkTruth({ simulated: false, realPg: true, hasProofGaps: false }),
      latest,
      detailForPipeline: latest,
      verification: { status: 1, backupRunId: rid } as BackupVerificationResponseDto,
      recoverability: {
        lastSuccessfulBackupAt: '2026-01-01T00:00:00Z',
      } as BackupRecoverabilitySummaryResponseDto,
      restoreLatest: restore,
      restoreExtended: { postRestoreContinuityChecksExecuted: true, postRestoreContinuityChecksPassed: true },
    });
    expect(m.highestFullyProvenLevel).toBe(4);
    expect(m.fiscalVerifiedSummary.passed).toBe(true);
  });

  it('L5/L6 show gap when no restore drill; app smoke not configured', () => {
    const m = buildDrProofPresentationModel({
      truth: mkTruth({}),
      latest: undefined,
      detailForPipeline: undefined,
      verification: undefined,
      recoverability: {},
      restoreLatest: undefined,
      restoreExtended: {},
    });
    expect(m.layers[5].state).toBe('gap');
    expect(m.layers[6].state).toBe('gap');
    expect(m.appRecoverySummary.state).toBe('not_configured');
  });
});

describe('mapDrProofScanTagToneToAntdTagColor', () => {
  it('maps scan tag tones to Ant Design Tag presets (no success/green)', () => {
    expect(mapDrProofScanTagToneToAntdTagColor('error')).toBe('red');
    expect(mapDrProofScanTagToneToAntdTagColor('warning')).toBe('orange');
    expect(mapDrProofScanTagToneToAntdTagColor('processing')).toBe('blue');
    expect(mapDrProofScanTagToneToAntdTagColor('default')).toBe('default');
  });
});
