import { describe, expect, it } from 'vitest';

import type { BackupExecutionModeResponseDto } from '@/features/backup-dr/logic/backupExecutionModeApi';
import {
  isRealModeSelectableNow,
  parseHypotheticalPgDumpHealthLevel,
  presentRealModeDiagnostics,
  realReadinessSummaryCopy,
} from '@/features/backup-dr/logic/backupRealModeReadinessPresentation';

const t = (k: string) => k;

describe('parseHypotheticalPgDumpHealthLevel', () => {
  it('parses known levels', () => {
    expect(parseHypotheticalPgDumpHealthLevel('Healthy')).toBe('Healthy');
    expect(parseHypotheticalPgDumpHealthLevel('Degraded')).toBe('Degraded');
    expect(parseHypotheticalPgDumpHealthLevel('Unhealthy')).toBe('Unhealthy');
  });
  it('returns empty for unknown', () => {
    expect(parseHypotheticalPgDumpHealthLevel('')).toBe('');
    expect(parseHypotheticalPgDumpHealthLevel('nope')).toBe('');
  });
});

describe('isRealModeSelectableNow', () => {
  it('reads RealPgDump row', () => {
    const d: BackupExecutionModeResponseDto = {
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
      selectableModes: [
        { userFacingMode: 'RealPgDump', internalMode: 'PostgreSqlPgDump', selectable: true },
      ],
      effectiveConfigurationHealth: {},
    };
    expect(isRealModeSelectableNow(d)).toBe(true);
    expect(
      isRealModeSelectableNow({
        ...d,
        selectableModes: [{ ...d.selectableModes[0], selectable: false }],
      })
    ).toBe(false);
  });
});

describe('presentRealModeDiagnostics', () => {
  it('sorts blocking before advisory within same category', () => {
    const presented = presentRealModeDiagnostics(
      [
        {
          code: 'BACKUP_SETUP_DEV_PG_RESTORE_CLIENT_MISSING_OR_BROKEN',
          severity: 'Warning',
          message: 'w',
          relatedConfigurationKeys: [],
        },
        {
          code: 'BACKUP_SETUP_PG_DUMP_STAGING_ROOT_MISSING',
          severity: 'Error',
          message: 'e',
          relatedConfigurationKeys: ['Backup:ArtifactStagingRoot'],
        },
      ],
      t
    );
    expect(presented[0].code).toBe('BACKUP_SETUP_PG_DUMP_STAGING_ROOT_MISSING');
    expect(presented[0].tier).toBe('blocking');
    expect(presented[1].tier).toBe('advisory');
  });
});

describe('realReadinessSummaryCopy', () => {
  it('blocked when not selectable', () => {
    const x = realReadinessSummaryCopy('Healthy', false, t);
    expect(x.alertType).toBe('error');
    expect(x.title).toContain('blocked');
  });
  it('ready when healthy and selectable', () => {
    const x = realReadinessSummaryCopy('Healthy', true, t);
    expect(x.alertType).toBe('success');
  });
  it('degraded warning when selectable', () => {
    const x = realReadinessSummaryCopy('Degraded', true, t);
    expect(x.alertType).toBe('warning');
  });
});
