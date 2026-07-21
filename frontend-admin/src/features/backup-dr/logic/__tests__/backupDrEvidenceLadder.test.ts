import { describe, expect, it } from 'vitest';

import { BackupRunResponseDtoStatus } from '@/api/generated/model/backupRunResponseDtoStatus';
import { RestoreVerificationRunResponseDtoStatus } from '@/api/generated/model/restoreVerificationRunResponseDtoStatus';
import { deriveBackupEvidenceLadder } from '@/features/backup-dr/logic/backupDrEvidenceLadder';

describe('deriveBackupEvidenceLadder', () => {
  it('Fake success: stub headline and non-stub step fails', () => {
    const m = deriveBackupEvidenceLadder({
      latest: {
        status: BackupRunResponseDtoStatus.NUMBER_3,
        id: 'r1',
        adapterKind: 'Fake',
      } as never,
      detailForPipeline: { artifacts: [], isSimulatedExecution: true } as never,
      verification: undefined,
      restoreLatest: undefined,
      recoverabilitySummary: undefined,
      simulatedEvidence: true,
      realPostgreSqlLogicalDumpConfigured: false,
    });
    expect(m.headlineKey).toBe('backupDr.evidence.headline.stubPipeline');
    const nonStub = m.steps.find((s) => s.id === 'non_stub_run');
    expect(nonStub?.status).toBe('fail');
  });

  it('PgDump + list OK + drill OK + full proofs: strong headline', () => {
    const m = deriveBackupEvidenceLadder({
      latest: {
        status: BackupRunResponseDtoStatus.NUMBER_3,
        id: 'r1',
        adapterKind: 'PgDump',
      } as never,
      detailForPipeline: {
        isSimulatedExecution: false,
        artifacts: [{ artifactType: 0, isFilePresentForDownload: true, byteSize: 50_000 }],
      } as never,
      verification: { status: 1, backupRunId: 'r1' } as never,
      restoreLatest: {
        status: RestoreVerificationRunResponseDtoStatus.NUMBER_2,
        dumpInspectionPassed: true,
        restoreAttemptExecuted: false,
      } as never,
      recoverabilitySummary: {
        lastSuccessfulBackupAt: '2026-01-01',
        lastSuccessfulArtifactVerificationAt: '2026-01-01',
        lastSuccessfulRestoreProofAt: '2026-01-01',
      } as never,
      simulatedEvidence: false,
      realPostgreSqlLogicalDumpConfigured: true,
    });
    expect(m.headlineKey).toBe('backupDr.evidence.headline.strongWithinApi');
    expect(m.steps.find((s) => s.id === 'dump_list')?.status).toBe('pass');
    expect(m.backendSignalGaps.length).toBeGreaterThan(0);
  });

  it('PgDump + latest drill failed: warns instead of strong headline', () => {
    const m = deriveBackupEvidenceLadder({
      latest: {
        status: BackupRunResponseDtoStatus.NUMBER_3,
        id: 'r1',
        adapterKind: 'PgDump',
      } as never,
      detailForPipeline: {
        isSimulatedExecution: false,
        artifacts: [{ artifactType: 0, isFilePresentForDownload: true, byteSize: 50_000 }],
      } as never,
      verification: { status: 1, backupRunId: 'r1' } as never,
      restoreLatest: {
        status: RestoreVerificationRunResponseDtoStatus.NUMBER_3,
        dumpInspectionPassed: false,
        restoreAttemptExecuted: true,
        restoreAttemptPassed: false,
      } as never,
      recoverabilitySummary: {
        lastSuccessfulBackupAt: '2026-01-01',
        lastSuccessfulArtifactVerificationAt: '2026-01-01',
        lastSuccessfulRestoreProofAt: '2026-01-01',
      } as never,
      simulatedEvidence: false,
      realPostgreSqlLogicalDumpConfigured: true,
    });
    expect(m.headlineKey).toBe('backupDr.evidence.headline.latestDrillFailed');
  });
});
