import { describe, expect, it } from 'vitest';

import type {
  BackupArtifactPipelinePolicyResponseDto,
  BackupPipelineSnapshotDto,
  BackupRunResponseDto,
} from '@/api/generated/model';
import { BackupArtifactResponseDtoArtifactType } from '@/api/generated/model/backupArtifactResponseDtoArtifactType';
import { BackupArtifactResponseDtoLifecycleState } from '@/api/generated/model/backupArtifactResponseDtoLifecycleState';
import type {
  DerivedPipelineStepId,
  DerivedPipelineStepState,
} from '@/features/backup-dr/logic/backupPipelineDerived';
import {
  SERVER_PIPELINE_PROJECTION_VERSION,
  deriveBackupPipelineSteps,
  formatRunDurationMs,
  mapServerPipelineStatus,
  pipelineSnapshotToDerivedSteps,
  resolveBackupPipelineStepsForUi,
  sumLogicalDumpBytes,
} from '@/features/backup-dr/logic/backupPipelineDerived';

const RUN_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';

function sm(
  steps: ReturnType<typeof deriveBackupPipelineSteps>
): Record<DerivedPipelineStepId, DerivedPipelineStepState> {
  return Object.fromEntries(steps.map((s) => [s.id, s.state])) as Record<
    DerivedPipelineStepId,
    DerivedPipelineStepState
  >;
}

function policyExternalOn(): BackupArtifactPipelinePolicyResponseDto {
  return {
    willRunExternalArchiveAfterStagingVerificationWhenEligible: true,
    externalArchiveRootConfigured: true,
  };
}

function policyExternalOff(): BackupArtifactPipelinePolicyResponseDto {
  return {
    willRunExternalArchiveAfterStagingVerificationWhenEligible: false,
    externalArchiveRootConfigured: false,
  };
}

describe('deriveBackupPipelineSteps', () => {
  it('returns empty when run id missing', () => {
    expect(
      deriveBackupPipelineSteps({ status: 0 } as BackupRunResponseDto, null, undefined)
    ).toEqual([]);
  });

  it('1) queued run: step queued running, worker pending', () => {
    const run: BackupRunResponseDto = { id: RUN_ID, status: 0 };
    const m = sm(deriveBackupPipelineSteps(run, null, policyExternalOn()));
    expect(m.queued).toBe('running');
    expect(m.workerRunning).toBe('pending');
    expect(m.dumpComplete).toBe('pending');
    // Harici aşama mantıksal dump yokken henüz uygulanamaz (skipped değil: policy bekleniyor).
    expect(m.externalCopy).toBe('pending');
    expect(m.externalChecksum).toBe('pending');
  });

  it('2) running run: worker running, dump/artifact still pending', () => {
    const run: BackupRunResponseDto = { id: RUN_ID, status: 1 };
    const m = sm(deriveBackupPipelineSteps(run, null, policyExternalOff()));
    expect(m.queued).toBe('success');
    expect(m.workerRunning).toBe('running');
    expect(m.artifactCreated).toBe('pending');
  });

  it('3) awaiting verification: dump+artifact success, verification running, external pending when expected', () => {
    const run: BackupRunResponseDto = { id: RUN_ID, status: 2 };
    const detail: BackupRunResponseDto = {
      id: RUN_ID,
      status: 2,
      artifacts: [
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
          lifecycleState: BackupArtifactResponseDtoLifecycleState.NUMBER_0,
        },
      ],
      verifications: [
        {
          backupRunId: RUN_ID,
          status: 0,
          startedAt: '2026-01-01T00:00:00Z',
        },
      ],
    };
    const m = sm(deriveBackupPipelineSteps(run, detail, policyExternalOn()));
    expect(m.dumpComplete).toBe('success');
    expect(m.artifactCreated).toBe('success');
    expect(m.artifactVerification).toBe('running');
    expect(m.externalCopy).toBe('pending');
  });

  it('4) succeeded with manifest + verification + external verified', () => {
    const run: BackupRunResponseDto = { id: RUN_ID, status: 3 };
    const detail: BackupRunResponseDto = {
      id: RUN_ID,
      status: 3,
      artifacts: [
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
          lifecycleState: BackupArtifactResponseDtoLifecycleState.NUMBER_2,
        },
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_4,
          lifecycleState: BackupArtifactResponseDtoLifecycleState.NUMBER_2,
        },
      ],
      verifications: [{ backupRunId: RUN_ID, status: 1, startedAt: '2026-01-01T00:00:00Z' }],
    };
    const m = sm(deriveBackupPipelineSteps(run, detail, policyExternalOn()));
    expect(m.artifactVerification).toBe('success');
    expect(m.manifestCreated).toBe('success');
    expect(m.externalCopy).toBe('success');
    expect(m.externalChecksum).toBe('success');
  });

  it('5) verification failed because external archive failed (lifecycle + failed verification)', () => {
    const run: BackupRunResponseDto = { id: RUN_ID, status: 5 };
    const detail: BackupRunResponseDto = {
      id: RUN_ID,
      status: 5,
      artifacts: [
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
          lifecycleState: BackupArtifactResponseDtoLifecycleState.NUMBER_3,
        },
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_4,
          lifecycleState: BackupArtifactResponseDtoLifecycleState.NUMBER_3,
        },
      ],
      verifications: [{ backupRunId: RUN_ID, status: 2, startedAt: '2026-01-01T00:00:00Z' }],
    };
    const m = sm(deriveBackupPipelineSteps(run, detail, policyExternalOn()));
    expect(m.artifactVerification).toBe('failed');
    expect(m.externalCopy).toBe('degraded');
    expect(m.externalChecksum).toBe('failed');
  });

  it('6) external archive skipped by policy (not expected)', () => {
    const run: BackupRunResponseDto = { id: RUN_ID, status: 3 };
    const detail: BackupRunResponseDto = {
      id: RUN_ID,
      status: 3,
      artifacts: [
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
          lifecycleState: BackupArtifactResponseDtoLifecycleState.NUMBER_1,
        },
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_4,
          lifecycleState: BackupArtifactResponseDtoLifecycleState.NUMBER_1,
        },
      ],
      verifications: [{ backupRunId: RUN_ID, status: 1, startedAt: '2026-01-01T00:00:00Z' }],
    };
    const m = sm(deriveBackupPipelineSteps(run, detail, policyExternalOff()));
    expect(m.externalCopy).toBe('skipped');
    expect(m.externalChecksum).toBe('skipped');
  });

  it('7) missing detail: uses run.artifacts if present; otherwise conservative pending states', () => {
    const runMinimal: BackupRunResponseDto = { id: RUN_ID, status: 3 };
    const m1 = sm(deriveBackupPipelineSteps(runMinimal, null, policyExternalOff()));
    expect(m1.manifestCreated).toBe('pending');
    expect(m1.artifactCreated).toBe('pending');

    const runWithChildren: BackupRunResponseDto = {
      id: RUN_ID,
      status: 3,
      artifacts: [
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
          lifecycleState: BackupArtifactResponseDtoLifecycleState.NUMBER_1,
        },
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_4,
          lifecycleState: BackupArtifactResponseDtoLifecycleState.NUMBER_1,
        },
      ],
      verifications: [{ backupRunId: RUN_ID, status: 1, startedAt: '2026-01-01T00:00:00Z' }],
    };
    const m2 = sm(deriveBackupPipelineSteps(runWithChildren, null, policyExternalOff()));
    expect(m2.manifestCreated).toBe('success');
    expect(m2.artifactCreated).toBe('success');
  });

  it('9) cancelled: deterministic skipped tail', () => {
    const run: BackupRunResponseDto = { id: RUN_ID, status: 6 };
    const m = sm(deriveBackupPipelineSteps(run, null, policyExternalOn()));
    expect(m.queued).toBe('success');
    expect(m.workerRunning).toBe('skipped');
    expect(m.externalCopy).toBe('skipped');
  });

  it('9b) succeeded without verification row: artifactVerification pending (not running — no false activity)', () => {
    const run: BackupRunResponseDto = { id: RUN_ID, status: 3 };
    const detail: BackupRunResponseDto = {
      id: RUN_ID,
      status: 3,
      artifacts: [
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
          lifecycleState: BackupArtifactResponseDtoLifecycleState.NUMBER_1,
        },
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_4,
          lifecycleState: BackupArtifactResponseDtoLifecycleState.NUMBER_1,
        },
      ],
      verifications: [],
    };
    const m = sm(deriveBackupPipelineSteps(run, detail, policyExternalOff()));
    expect(m.artifactVerification).toBe('pending');
  });

  it('external eligible but success left at StagingVerified: external steps skipped (not falsely green)', () => {
    const run: BackupRunResponseDto = { id: RUN_ID, status: 3 };
    const detail: BackupRunResponseDto = {
      id: RUN_ID,
      status: 3,
      artifacts: [
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
          lifecycleState: BackupArtifactResponseDtoLifecycleState.NUMBER_1,
        },
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_4,
          lifecycleState: BackupArtifactResponseDtoLifecycleState.NUMBER_1,
        },
      ],
      verifications: [{ backupRunId: RUN_ID, status: 1, startedAt: '2026-01-01T00:00:00Z' }],
    };
    const m = sm(deriveBackupPipelineSteps(run, detail, policyExternalOn()));
    expect(m.externalCopy).toBe('skipped');
    expect(m.externalChecksum).toBe('skipped');
  });

  it('pickPrimaryVerification prefers latest completedAt among rows', () => {
    const run: BackupRunResponseDto = { id: RUN_ID, status: 3 };
    const detail: BackupRunResponseDto = {
      id: RUN_ID,
      status: 3,
      artifacts: [
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
          lifecycleState: BackupArtifactResponseDtoLifecycleState.NUMBER_1,
        },
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_4,
          lifecycleState: BackupArtifactResponseDtoLifecycleState.NUMBER_1,
        },
      ],
      verifications: [
        {
          backupRunId: RUN_ID,
          status: 2,
          startedAt: '2026-01-01T00:00:00Z',
          completedAt: '2026-01-01T00:01:00Z',
        },
        {
          backupRunId: RUN_ID,
          status: 1,
          startedAt: '2026-01-01T00:00:00Z',
          completedAt: '2026-01-01T00:02:00Z',
        },
      ],
    };
    const m = sm(deriveBackupPipelineSteps(run, detail, policyExternalOff()));
    expect(m.artifactVerification).toBe('success');
  });
});

describe('pipelineSnapshotToDerivedSteps', () => {
  it('maps not_required to skipped for stepper', () => {
    expect(mapServerPipelineStatus('not_required')).toBe('skipped');
  });

  it('returns null when steps missing or wrong count', () => {
    expect(pipelineSnapshotToDerivedSteps(null)).toBeNull();
    expect(pipelineSnapshotToDerivedSteps({ steps: [] })).toBeNull();
    expect(
      pipelineSnapshotToDerivedSteps({
        steps: [{ key: 'queued', status: 'running' }],
      } as BackupPipelineSnapshotDto)
    ).toBeNull();
  });

  it('accepts eight server steps with known keys', () => {
    const snap: BackupPipelineSnapshotDto = {
      steps: [
        { key: 'queued', status: 'success' },
        { key: 'workerRunning', status: 'success' },
        { key: 'dumpComplete', status: 'success' },
        { key: 'artifactCreated', status: 'success' },
        { key: 'artifactVerification', status: 'success' },
        { key: 'manifestCreated', status: 'success' },
        { key: 'externalCopy', status: 'not_required' },
        { key: 'externalChecksum', status: 'not_required' },
      ],
    };
    const steps = pipelineSnapshotToDerivedSteps(snap);
    expect(steps).toHaveLength(8);
    expect(steps![6].state).toBe('skipped');
    expect(steps![7].state).toBe('skipped');
  });

  it('returns null when step keys are correct set but wrong order', () => {
    const snap: BackupPipelineSnapshotDto = {
      steps: [
        { key: 'workerRunning', status: 'pending' },
        { key: 'queued', status: 'success' },
        { key: 'dumpComplete', status: 'success' },
        { key: 'artifactCreated', status: 'success' },
        { key: 'artifactVerification', status: 'success' },
        { key: 'manifestCreated', status: 'success' },
        { key: 'externalCopy', status: 'not_required' },
        { key: 'externalChecksum', status: 'not_required' },
      ],
    };
    expect(pipelineSnapshotToDerivedSteps(snap)).toBeNull();
  });
});

describe('resolveBackupPipelineStepsForUi', () => {
  const eightSteps: BackupPipelineSnapshotDto['steps'] = [
    { key: 'queued', status: 'success' },
    { key: 'workerRunning', status: 'success' },
    { key: 'dumpComplete', status: 'success' },
    { key: 'artifactCreated', status: 'success' },
    { key: 'artifactVerification', status: 'success' },
    { key: 'manifestCreated', status: 'success' },
    { key: 'externalCopy', status: 'not_required' },
    { key: 'externalChecksum', status: 'not_required' },
  ];

  it('prefers server projection when snapshot is valid', () => {
    const run: BackupRunResponseDto = { id: RUN_ID, status: 3 };
    const detail: BackupRunResponseDto = {
      id: RUN_ID,
      status: 3,
      pipeline: { projectionVersion: SERVER_PIPELINE_PROJECTION_VERSION, steps: eightSteps },
    };
    const r = resolveBackupPipelineStepsForUi(run, detail, policyExternalOff(), {
      allowClientFallback: true,
    });
    expect(r.source).toBe('server_projection');
    expect(r.projectionVersionMismatch).toBe(false);
    expect(r.steps).toHaveLength(8);
  });

  it('uses client fallback when snapshot missing', () => {
    const run: BackupRunResponseDto = { id: RUN_ID, status: 0 };
    const r = resolveBackupPipelineStepsForUi(run, { id: RUN_ID, status: 0 }, policyExternalOff(), {
      allowClientFallback: true,
    });
    expect(r.source).toBe('client_fallback');
    expect(r.projectionVersionMismatch).toBe(false);
    expect(r.steps.length).toBeGreaterThan(0);
  });

  it('marks version mismatch and falls back when projectionVersion unknown', () => {
    const run: BackupRunResponseDto = { id: RUN_ID, status: 3 };
    const detail: BackupRunResponseDto = {
      id: RUN_ID,
      status: 3,
      pipeline: { projectionVersion: '2099-01-01', steps: eightSteps },
    };
    const r = resolveBackupPipelineStepsForUi(run, detail, policyExternalOff(), {
      allowClientFallback: true,
    });
    expect(r.source).toBe('client_fallback');
    expect(r.projectionVersionMismatch).toBe(true);
  });

  it('blocks client fallback when disabled and snapshot unusable', () => {
    const run: BackupRunResponseDto = { id: RUN_ID, status: 0 };
    const r = resolveBackupPipelineStepsForUi(run, { id: RUN_ID, status: 0 }, policyExternalOff(), {
      allowClientFallback: false,
    });
    expect(r.source).toBe('client_fallback_blocked');
    expect(r.steps).toEqual([]);
    expect(r.projectionVersionMismatch).toBe(false);
  });

  it('blocks when projection version is unsupported and client fallback is off (no silent client switch)', () => {
    const run: BackupRunResponseDto = { id: RUN_ID, status: 3 };
    const detail: BackupRunResponseDto = {
      id: RUN_ID,
      status: 3,
      pipeline: { projectionVersion: '2099-01-01', steps: eightSteps },
    };
    const r = resolveBackupPipelineStepsForUi(run, detail, policyExternalOff(), {
      allowClientFallback: false,
    });
    expect(r.source).toBe('client_fallback_blocked');
    expect(r.projectionVersionMismatch).toBe(true);
    expect(r.steps).toEqual([]);
  });
});

describe('SERVER_PIPELINE_PROJECTION_VERSION', () => {
  it('matches BackupPipelineProjector.ProjectionVersion in backend (manual sync)', () => {
    expect(SERVER_PIPELINE_PROJECTION_VERSION).toBe('2026-03-28');
  });
});

describe('formatRunDurationMs / sumLogicalDumpBytes', () => {
  it('duration invalid order → undefined', () => {
    expect(formatRunDurationMs('2026-01-02T00:00:00Z', '2026-01-01T00:00:00Z')).toBeUndefined();
  });

  it('sumLogicalDumpBytes reads logical dump artifact', () => {
    expect(
      sumLogicalDumpBytes([
        { artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0, byteSize: 42 },
        { artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_4, byteSize: 1 },
      ])
    ).toBe(42);
  });
});
