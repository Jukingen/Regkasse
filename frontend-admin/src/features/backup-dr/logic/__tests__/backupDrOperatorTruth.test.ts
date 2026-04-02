import { describe, expect, it } from 'vitest';
import { buildOperatorTruthBanner, hasRecoverabilityProofGaps } from '@/features/backup-dr/logic/backupDrOperatorTruth';

describe('buildOperatorTruthBanner', () => {
  const t = (k: string) => k;

  it('flags simulated latest success as informational (expected stub behavior)', () => {
    const r = buildOperatorTruthBanner({
      t,
      health: undefined,
      healthLv: '',
      restoreReady: undefined,
      restoreLv: '',
      latest: { status: 3, adapterKind: 'Fake' } as never,
      detailForPipeline: null,
      verification: undefined,
      externalCopyVariant: 'unknown',
      restoreLatest: undefined,
    });
    expect(r.info).toContain('backupDr.banner.latestRunSimulatedNotProduction');
    expect(r.critical).toEqual([]);
  });

  it('includes restore drill failure in critical', () => {
    const r = buildOperatorTruthBanner({
      t,
      health: undefined,
      healthLv: '',
      restoreReady: undefined,
      restoreLv: '',
      latest: undefined,
      detailForPipeline: null,
      verification: undefined,
      externalCopyVariant: 'unknown',
      restoreLatest: { status: 3, failureCode: 'X' } as never,
    });
    expect(r.critical.some((x) => x.includes('backupDr.restoreVerification.drillFailed'))).toBe(true);
  });
});

describe('hasRecoverabilityProofGaps', () => {
  it('true when any proof timestamp missing', () => {
    expect(
      hasRecoverabilityProofGaps({
        lastSuccessfulBackupAt: '2026-01-01',
        lastSuccessfulArtifactVerificationAt: null,
        lastSuccessfulRestoreProofAt: null,
      } as never),
    ).toBe(true);
  });

  it('false when all three timestamps present', () => {
    expect(
      hasRecoverabilityProofGaps({
        lastSuccessfulBackupAt: '2026-01-01',
        lastSuccessfulArtifactVerificationAt: '2026-01-01',
        lastSuccessfulRestoreProofAt: '2026-01-01',
      } as never),
    ).toBe(false);
  });
});
