import { describe, expect, it } from 'vitest';

import {
  deriveBackupExecutionModeTruth,
  unloadedBackupExecutionModeTruth,
} from '@/features/backup-dr/logic/backupDrExecutionModeTruth';
import type { BackupExecutionModeResponseDto } from '@/features/backup-dr/logic/backupExecutionModeApi';

function dto(p: Partial<BackupExecutionModeResponseDto>): BackupExecutionModeResponseDto {
  return {
    storedMode: 'InheritFromConfiguration',
    requestedUserFacingMode: 'UseConfigurationDefault',
    configurationDefaultUserFacingMode: 'Fake',
    effectiveUserFacingMode: 'Fake',
    recommendedFallbackUserFacingMode: null,
    adapterKindIfConfigurationDefaultOnly: 'Fake',
    effectiveModeResolutionSummaryEnglish: '',
    configurationExecutionAdapterKind: 'Fake',
    effectiveExecutionAdapterKind: 'Fake',
    effectiveModeRunnable: true,
    hypotheticalPgDumpHealthLevel: 'Healthy',
    blockers: [],
    realModeBlockingDiagnostics: [],
    selectableModes: [],
    effectiveConfigurationHealth: {},
    ...p,
  };
}

describe('deriveBackupExecutionModeTruth', () => {
  it('returns unloaded shape when dto missing', () => {
    expect(deriveBackupExecutionModeTruth(undefined)).toEqual(unloadedBackupExecutionModeTruth);
  });

  it('flags requestedRealButBlocked', () => {
    const t = deriveBackupExecutionModeTruth(
      dto({
        requestedUserFacingMode: 'RealPgDump',
        effectiveExecutionAdapterKind: 'PgDump',
        effectiveUserFacingMode: 'RealPgDump',
        effectiveModeRunnable: false,
      })
    );
    expect(t.loaded).toBe(true);
    expect(t.requestedRealButBlocked).toBe(true);
    expect(t.effectiveIsPgDumpAdapter).toBe(true);
    expect(t.fallbackBehavior).toBe('none');
  });

  it('flags operator guidance when recommended fallback set', () => {
    const t = deriveBackupExecutionModeTruth(
      dto({
        recommendedFallbackUserFacingMode: 'UseConfigurationDefault',
      })
    );
    expect(t.fallbackBehavior).toBe('operator_guidance_only');
    expect(t.recommendedFallbackUserFacingMode).toBe('UseConfigurationDefault');
  });

  it('flags requestedRealButEffectiveSimulated', () => {
    const t = deriveBackupExecutionModeTruth(
      dto({
        requestedUserFacingMode: 'RealPgDump',
        effectiveExecutionAdapterKind: 'Fake',
        effectiveUserFacingMode: 'Fake',
      })
    );
    expect(t.requestedRealButEffectiveSimulated).toBe(true);
  });

  it('flags requestedFakeButEffectivePgDump (honest mismatch)', () => {
    const t = deriveBackupExecutionModeTruth(
      dto({
        requestedUserFacingMode: 'Fake',
        effectiveExecutionAdapterKind: 'PgDump',
        effectiveUserFacingMode: 'RealPgDump',
      })
    );
    expect(t.requestedFakeButEffectivePgDump).toBe(true);
    expect(t.effectiveIsPgDumpAdapter).toBe(true);
  });
});
