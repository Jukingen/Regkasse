import { describe, expect, it } from 'vitest';
import type { BackupArtifactResponseDto, RestoreVerificationRunResponseDto } from '@/api/generated/model';
import { BackupArtifactResponseDtoArtifactType } from '@/api/generated/model/backupArtifactResponseDtoArtifactType';
import { BackupArtifactResponseDtoLifecycleState } from '@/api/generated/model/backupArtifactResponseDtoLifecycleState';
import {
  configurationHealthSummaryI18nKey,
  externalCopyVariantToI18nKey,
  mapArtifactsToExternalCopyVariant,
  mapBackupRunStatusAntdColor,
  mapConfigurationHealthLevel,
  mapDumpInspectionTriState,
  mapRestoreVerificationPhases,
  mapRestoreVerificationStatusAntdColor,
  normalizeHealthLevelString,
} from '@/features/backup-dr/logic/backupDrMappers';

describe('mapConfigurationHealthLevel / configurationHealthSummaryI18nKey', () => {
  it('8) unknown / empty / whitespace', () => {
    expect(mapConfigurationHealthLevel(undefined)).toBe('unknown');
    expect(mapConfigurationHealthLevel('')).toBe('unknown');
    expect(mapConfigurationHealthLevel('   ')).toBe('unknown');
    expect(configurationHealthSummaryI18nKey(undefined)).toBe('backupDr.summary.unknown');
  });

  it('8) unknown non-standard API string maps to unknown bucket', () => {
    expect(mapConfigurationHealthLevel('FooBar')).toBe('unknown');
    expect(configurationHealthSummaryI18nKey('FooBar')).toBe('backupDr.summary.unknown');
  });

  it('8) healthy / degraded / unhealthy case-insensitive', () => {
    expect(normalizeHealthLevelString('Healthy')).toBe('healthy');
    expect(mapConfigurationHealthLevel('HEALTHY')).toBe('healthy');
    expect(mapConfigurationHealthLevel('Degraded')).toBe('degraded');
    expect(mapConfigurationHealthLevel('Unhealthy')).toBe('unhealthy');
    expect(configurationHealthSummaryI18nKey('healthy')).toBe('backupDr.health.healthy');
    expect(configurationHealthSummaryI18nKey('degraded')).toBe('backupDr.health.degraded');
    expect(configurationHealthSummaryI18nKey('unhealthy')).toBe('backupDr.health.unhealthy');
  });
});

describe('mapBackupRunStatusAntdColor (recent runs)', () => {
  it('maps queued/running/await/success/fail/verify-fail/cancelled distinctly', () => {
    expect(mapBackupRunStatusAntdColor(0)).toBe('default');
    expect(mapBackupRunStatusAntdColor(1)).toBe('processing');
    expect(mapBackupRunStatusAntdColor(2)).toBe('warning');
    expect(mapBackupRunStatusAntdColor(3)).toBe('success');
    expect(mapBackupRunStatusAntdColor(4)).toBe('error');
    expect(mapBackupRunStatusAntdColor(5)).toBe('error');
    expect(mapBackupRunStatusAntdColor(6)).toBe('default');
    expect(mapBackupRunStatusAntdColor(undefined)).toBe('default');
  });
});

describe('mapRestoreVerificationStatusAntdColor', () => {
  it('maps restore drill statuses', () => {
    expect(mapRestoreVerificationStatusAntdColor(0)).toBe('processing');
    expect(mapRestoreVerificationStatusAntdColor(1)).toBe('processing');
    expect(mapRestoreVerificationStatusAntdColor(2)).toBe('success');
    expect(mapRestoreVerificationStatusAntdColor(3)).toBe('error');
    expect(mapRestoreVerificationStatusAntdColor(undefined)).toBe('processing');
  });
});

describe('mapArtifactsToExternalCopyVariant / i18n key', () => {
  it('unknown empty', () => {
    expect(mapArtifactsToExternalCopyVariant(undefined)).toBe('unknown');
    expect(externalCopyVariantToI18nKey('unknown')).toBe('backupDr.externalCopy.unknown');
  });

  it('verified / failed / staging / mixed', () => {
    const logical = (ls: number): BackupArtifactResponseDto => ({
      artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
      lifecycleState: ls as BackupArtifactResponseDtoLifecycleState,
    });
    expect(
      mapArtifactsToExternalCopyVariant([
        logical(BackupArtifactResponseDtoLifecycleState.NUMBER_2),
        {
          artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_4,
          lifecycleState: BackupArtifactResponseDtoLifecycleState.NUMBER_1,
        },
      ]),
    ).toBe('verified');

    expect(mapArtifactsToExternalCopyVariant([logical(BackupArtifactResponseDtoLifecycleState.NUMBER_3)])).toBe('failed');

    expect(
      mapArtifactsToExternalCopyVariant([logical(BackupArtifactResponseDtoLifecycleState.NUMBER_0)]),
    ).toBe('staging');

    expect(
      mapArtifactsToExternalCopyVariant([
        logical(BackupArtifactResponseDtoLifecycleState.NUMBER_3),
        logical(BackupArtifactResponseDtoLifecycleState.NUMBER_2),
      ]),
    ).toBe('mixed');
  });
});

describe('mapDumpInspectionTriState', () => {
  it('prefers dumpInspectionPassed then legacy pgRestoreListPassed', () => {
    expect(mapDumpInspectionTriState({ dumpInspectionPassed: true } as RestoreVerificationRunResponseDto)).toBe(true);
    expect(
      mapDumpInspectionTriState({ dumpInspectionPassed: null, pgRestoreListPassed: false } as RestoreVerificationRunResponseDto),
    ).toBe(false);
  });
});

describe('mapRestoreVerificationPhases', () => {
  it('10) success matrix: all positive paths', () => {
    const rr: RestoreVerificationRunResponseDto = {
      status: 2,
      dumpInspectionPassed: true,
      restoreAttemptExecuted: true,
      restoreAttemptPassed: true,
      fiscalSqlSkipped: false,
      fiscalSqlPassed: true,
      integrityChecksPassed: true,
    };
    expect(mapRestoreVerificationPhases(rr)).toEqual({
      dumpInspection: 'ok',
      restoreAttempt: 'ok',
      fiscalSql: 'ok',
      integrity: 'ok',
    });
  });

  it('11) partial failure matrix: dump fail', () => {
    const rr: RestoreVerificationRunResponseDto = {
      dumpInspectionPassed: false,
      restoreAttemptExecuted: false,
      fiscalSqlSkipped: true,
      integrityChecksPassed: undefined,
    };
    expect(mapRestoreVerificationPhases(rr)).toMatchObject({
      dumpInspection: 'fail',
      restoreAttempt: 'not_run',
      fiscalSql: 'skipped',
      integrity: 'unknown',
    });
  });

  it('11) restore attempt fail with executed flag', () => {
    const rr: RestoreVerificationRunResponseDto = {
      dumpInspectionPassed: true,
      restoreAttemptExecuted: true,
      restoreAttemptPassed: false,
      fiscalSqlSkipped: true,
      integrityChecksPassed: true,
    };
    expect(mapRestoreVerificationPhases(rr)).toMatchObject({
      dumpInspection: 'ok',
      restoreAttempt: 'fail',
      fiscalSql: 'skipped',
      integrity: 'ok',
    });
  });

  it('11) fiscal fail + integrity issues', () => {
    const rr: RestoreVerificationRunResponseDto = {
      dumpInspectionPassed: true,
      restoreAttemptExecuted: true,
      restoreAttemptPassed: true,
      fiscalSqlSkipped: false,
      fiscalSqlPassed: false,
      integrityChecksPassed: false,
    };
    expect(mapRestoreVerificationPhases(rr)).toMatchObject({
      fiscalSql: 'fail',
      integrity: 'fail',
    });
  });

  it('null run → all unknown', () => {
    expect(mapRestoreVerificationPhases(null)).toEqual({
      dumpInspection: 'unknown',
      restoreAttempt: 'unknown',
      fiscalSql: 'unknown',
      integrity: 'unknown',
    });
  });
});
