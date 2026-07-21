import { describe, expect, it } from 'vitest';

import { fakeSwitchNeedsStrongWarning } from '@/features/backup-dr/logic/backupDrExecutionModePresentation';
import { isRealRequestedNonRunnableState } from '@/features/backup-dr/logic/backupDrExecutionModeTruth';
import type { BackupExecutionModeResponseDto } from '@/features/backup-dr/logic/backupExecutionModeApi';

function dto(partial: Partial<BackupExecutionModeResponseDto>): BackupExecutionModeResponseDto {
  return {
    storedMode: 'PostgreSqlPgDump',
    requestedUserFacingMode: 'RealPgDump',
    configurationDefaultUserFacingMode: 'Fake',
    effectiveUserFacingMode: 'RealPgDump',
    recommendedFallbackUserFacingMode: null,
    adapterKindIfConfigurationDefaultOnly: 'Fake',
    effectiveModeResolutionSummaryEnglish: '',
    configurationExecutionAdapterKind: 'Fake',
    effectiveExecutionAdapterKind: 'PgDump',
    effectiveModeRunnable: false,
    hypotheticalPgDumpHealthLevel: 'Unhealthy',
    blockers: [],
    realModeBlockingDiagnostics: [],
    selectableModes: [],
    effectiveConfigurationHealth: {},
    ...partial,
  };
}

describe('isRealRequestedNonRunnableState', () => {
  it('is true when Real is requested, adapter PgDump, and not runnable', () => {
    expect(
      isRealRequestedNonRunnableState(
        dto({
          requestedUserFacingMode: 'RealPgDump',
          effectiveExecutionAdapterKind: 'PgDump',
          effectiveModeRunnable: false,
        })
      )
    ).toBe(true);
  });

  it('is false when runnable', () => {
    expect(
      isRealRequestedNonRunnableState(
        dto({
          requestedUserFacingMode: 'RealPgDump',
          effectiveExecutionAdapterKind: 'PgDump',
          effectiveModeRunnable: true,
        })
      )
    ).toBe(false);
  });
});

describe('fakeSwitchNeedsStrongWarning', () => {
  it('is false when Fake not selected', () => {
    expect(
      fakeSwitchNeedsStrongWarning('PostgreSqlPgDump', {
        userFacingMode: 'Fake',
        internalMode: 'SimulatedFake',
        selectable: true,
        blockReason: 'production',
      })
    ).toBe(false);
  });

  it('is true when Fake selectable and blockReason set', () => {
    expect(
      fakeSwitchNeedsStrongWarning('SimulatedFake', {
        userFacingMode: 'Fake',
        internalMode: 'SimulatedFake',
        selectable: true,
        blockReason: 'Allowed with explicit API confirmation when saving.',
      })
    ).toBe(true);
  });

  it('is false when Fake not selectable', () => {
    expect(
      fakeSwitchNeedsStrongWarning('SimulatedFake', {
        userFacingMode: 'Fake',
        internalMode: 'SimulatedFake',
        selectable: false,
        blockReason: 'blocked',
      })
    ).toBe(false);
  });
});
