/**
 * Manuel tetik onay metinleri — etkin mod / adaptör ve istenen-etkin sapmaları.
 */
import { describe, expect, it } from 'vitest';
import { buildManualActionsConfirmations } from '@/features/backup-dr/logic/backupManualActionsModePresentation';
import type { BackupExecutionModeTruth } from '@/features/backup-dr/logic/backupDrExecutionModeTruth';

function t(k: string, o?: Record<string, string | number>): string {
  if (o && Object.keys(o).length > 0) return `${k}:${JSON.stringify(o)}`;
  return k;
}

const baseEm = (p: Partial<BackupExecutionModeTruth>): BackupExecutionModeTruth => ({
  loaded: true,
  requestedUserFacingMode: 'RealPgDump',
  effectiveUserFacingMode: 'RealPgDump',
  configurationDefaultUserFacingMode: 'Fake',
  effectiveExecutionAdapterKind: 'PgDump',
  configurationExecutionAdapterKind: 'Fake',
  effectiveIsSimulatedAdapter: false,
  effectiveIsPgDumpAdapter: true,
  effectiveModeRunnable: true,
  requestedRealButBlocked: false,
  recommendedFallbackUserFacingMode: null,
  resolutionSummaryEnglish: '',
  requestedRealButEffectiveSimulated: false,
  requestedFakeButEffectivePgDump: false,
  fallbackBehavior: 'none',
  ...p,
});

describe('buildManualActionsConfirmations — effective mode in backup trigger copy', () => {
  it('includes effective execution line with effective user-facing mode and PgDump behavior line', () => {
    const c = buildManualActionsConfirmations(baseEm({}), null, t);
    expect(c.backupDescriptionParts.some((x) => x.includes('backupDr.manual.effectiveExecutionLine'))).toBe(true);
    expect(c.backupDescriptionParts.some((x) => x.includes('backupDr.manual.confirmBackupBehaviorRealPgDump'))).toBe(true);
  });

  it('uses simulated behavior line when effective adapter is Fake', () => {
    const c = buildManualActionsConfirmations(
      baseEm({
        effectiveExecutionAdapterKind: 'Fake',
        effectiveUserFacingMode: 'Fake',
        effectiveIsSimulatedAdapter: true,
        effectiveIsPgDumpAdapter: false,
      }),
      null,
      t,
    );
    expect(c.backupDescriptionParts.some((x) => x.includes('backupDr.manual.confirmBackupBehaviorSimulated'))).toBe(true);
  });

  it('adds mismatch paragraph when requested Real but effective simulated', () => {
    const c = buildManualActionsConfirmations(
      baseEm({
        requestedUserFacingMode: 'RealPgDump',
        effectiveUserFacingMode: 'Fake',
        effectiveExecutionAdapterKind: 'Fake',
        effectiveIsSimulatedAdapter: true,
        effectiveIsPgDumpAdapter: false,
        requestedRealButEffectiveSimulated: true,
      }),
      null,
      t,
    );
    expect(
      c.backupDescriptionParts.some((x) => x.includes('backupDr.manual.confirmBackupMismatchRequestedRealEffectiveSimulated')),
    ).toBe(true);
    expect(c.cardAlert?.severity).toBe('error');
    expect(c.cardAlert?.message).toContain('backupDr.manual.cardAlertRequestedRealEffectiveSimulated');
  });

  it('adds blocked paragraph and warning card alert when requested Real is blocked', () => {
    const c = buildManualActionsConfirmations(
      baseEm({
        requestedRealButBlocked: true,
        effectiveModeRunnable: false,
      }),
      null,
      t,
    );
    expect(c.backupDescriptionParts.some((x) => x.includes('backupDr.manual.confirmBackupMismatchRequestedRealBlocked'))).toBe(
      true,
    );
    expect(c.cardAlert?.severity).toBe('warning');
    expect(c.cardAlert?.message).toContain('backupDr.manual.cardAlertRequestedRealBlocked');
  });
});
