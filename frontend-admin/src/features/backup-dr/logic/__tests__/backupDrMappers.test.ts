import { describe, expect, it } from 'vitest';

import type {
  BackupArtifactResponseDto,
  RestoreVerificationRunResponseDto,
} from '@/api/generated/model';
import { BackupArtifactResponseDtoArtifactType } from '@/api/generated/model/backupArtifactResponseDtoArtifactType';
import { BackupArtifactResponseDtoLifecycleState } from '@/api/generated/model/backupArtifactResponseDtoLifecycleState';
import {
  computeEffectiveRestoreReadinessLevel,
  configurationHealthSummaryI18nKey,
  externalCopyVariantToI18nKey,
  healthStatisticValueStyle,
  isSimulatedBackupAdapterKind,
  mapArtifactsToExternalCopyVariant,
  mapBackupRunStatusAntdColor,
  mapConfigurationHealthLevel,
  mapDumpInspectionTriState,
  mapRestoreVerificationPhases,
  mapRestoreVerificationStatusAntdColor,
  normalizeHealthLevelString,
  restoreReadinessStatisticValueStyle,
} from '@/features/backup-dr/logic/backupDrMappers';

describe('computeEffectiveRestoreReadinessLevel', () => {
  it('downgrades healthy to degraded when latest success is simulated', () => {
    expect(
      computeEffectiveRestoreReadinessLevel({
        apiLevel: 'healthy',
        realPostgreSqlLogicalDumpConfiguredHealth: true,
        realPostgreSqlLogicalDumpConfiguredRecoverability: true,
        latestBackupStatus: 3,
        isLatestRunSimulatedExecution: true,
        latestAdapterKind: 'PgDump',
      })
    ).toBe('degraded');
  });

  it('downgrades when simulated flag is undefined but adapter_kind is Fake (status/latest path)', () => {
    expect(
      computeEffectiveRestoreReadinessLevel({
        apiLevel: 'healthy',
        realPostgreSqlLogicalDumpConfiguredHealth: true,
        realPostgreSqlLogicalDumpConfiguredRecoverability: true,
        latestBackupStatus: 3,
        isLatestRunSimulatedExecution: undefined,
        latestAdapterKind: 'Fake',
      })
    ).toBe('degraded');
  });

  it('downgrades healthy to degraded when health reports no real pg_dump', () => {
    expect(
      computeEffectiveRestoreReadinessLevel({
        apiLevel: 'healthy',
        realPostgreSqlLogicalDumpConfiguredHealth: false,
        realPostgreSqlLogicalDumpConfiguredRecoverability: true,
        latestBackupStatus: 3,
        isLatestRunSimulatedExecution: false,
        latestAdapterKind: 'PgDump',
      })
    ).toBe('degraded');
  });

  it('downgrades healthy to degraded when recoverability reports no real pg_dump', () => {
    expect(
      computeEffectiveRestoreReadinessLevel({
        apiLevel: 'healthy',
        realPostgreSqlLogicalDumpConfiguredHealth: true,
        realPostgreSqlLogicalDumpConfiguredRecoverability: false,
        latestBackupStatus: 3,
        isLatestRunSimulatedExecution: false,
        latestAdapterKind: 'PgDump',
      })
    ).toBe('degraded');
  });

  it('does not downgrade when API is unhealthy', () => {
    expect(
      computeEffectiveRestoreReadinessLevel({
        apiLevel: 'unhealthy',
        realPostgreSqlLogicalDumpConfiguredHealth: false,
        realPostgreSqlLogicalDumpConfiguredRecoverability: true,
        latestBackupStatus: 3,
        isLatestRunSimulatedExecution: false,
        latestAdapterKind: 'PgDump',
      })
    ).toBe('unhealthy');
  });

  it('returns api level unchanged when healthy and real pg_dump and latest not simulated', () => {
    expect(
      computeEffectiveRestoreReadinessLevel({
        apiLevel: 'healthy',
        realPostgreSqlLogicalDumpConfiguredHealth: true,
        realPostgreSqlLogicalDumpConfiguredRecoverability: true,
        latestBackupStatus: 3,
        isLatestRunSimulatedExecution: false,
        latestAdapterKind: 'PgDump',
      })
    ).toBe('healthy');
  });

  it('downgrades healthy to degraded when execution-mode forces Fake adapter', () => {
    expect(
      computeEffectiveRestoreReadinessLevel({
        apiLevel: 'healthy',
        realPostgreSqlLogicalDumpConfiguredHealth: true,
        realPostgreSqlLogicalDumpConfiguredRecoverability: true,
        latestBackupStatus: 3,
        isLatestRunSimulatedExecution: false,
        latestAdapterKind: 'PgDump',
        executionModeUsesSimulatedAdapter: true,
      })
    ).toBe('degraded');
  });
});

describe('isSimulatedBackupAdapterKind', () => {
  it('detects Fake and ProductionStub', () => {
    expect(isSimulatedBackupAdapterKind('Fake')).toBe(true);
    expect(isSimulatedBackupAdapterKind('ProductionStub')).toBe(true);
    expect(isSimulatedBackupAdapterKind('PgDump')).toBe(false);
  });
});

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

describe('restoreReadinessStatisticValueStyle', () => {
  it('uses blue for healthy to avoid green DR misread', () => {
    expect(restoreReadinessStatisticValueStyle('healthy')).toEqual({ color: '#1677ff' });
  });
});

describe('healthStatisticValueStyle', () => {
  it('uses blue for healthy API-summary configuration signal', () => {
    expect(healthStatisticValueStyle('healthy')).toEqual({ color: '#1677ff' });
  });
});

describe('mapBackupRunStatusAntdColor (recent runs)', () => {
  it('maps queued/running/await/success/fail/verify-fail/cancelled distinctly', () => {
    expect(mapBackupRunStatusAntdColor(0)).toBe('default');
    expect(mapBackupRunStatusAntdColor(1)).toBe('processing');
    expect(mapBackupRunStatusAntdColor(2)).toBe('warning');
    expect(mapBackupRunStatusAntdColor(3)).toBe('blue');
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
    expect(mapRestoreVerificationStatusAntdColor(2)).toBe('cyan');
    expect(mapRestoreVerificationStatusAntdColor(3)).toBe('error');
    expect(mapRestoreVerificationStatusAntdColor(undefined)).toBe('processing');
  });
});

describe('mapArtifactsToExternalCopyVariant / i18n key', () => {
  it('unknown empty', () => {
    expect(mapArtifactsToExternalCopyVariant(undefined)).toBe('unknown');
    expect(externalCopyVariantToI18nKey('unknown')).toBe('backupDr.externalCopy.unknown');
    expect(externalCopyVariantToI18nKey('externalLifecycleOk')).toBe(
      'backupDr.externalCopy.externalLifecycleOk'
    );
  });

  it('externalLifecycleOk / failed / staging / mixed', () => {
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
      ])
    ).toBe('externalLifecycleOk');

    expect(
      mapArtifactsToExternalCopyVariant([logical(BackupArtifactResponseDtoLifecycleState.NUMBER_3)])
    ).toBe('failed');

    expect(
      mapArtifactsToExternalCopyVariant([logical(BackupArtifactResponseDtoLifecycleState.NUMBER_0)])
    ).toBe('staging');

    expect(
      mapArtifactsToExternalCopyVariant([
        logical(BackupArtifactResponseDtoLifecycleState.NUMBER_3),
        logical(BackupArtifactResponseDtoLifecycleState.NUMBER_2),
      ])
    ).toBe('mixed');
  });
});

describe('mapDumpInspectionTriState', () => {
  it('prefers dumpInspectionPassed then pgRestoreListExitCode (0 = pass)', () => {
    expect(
      mapDumpInspectionTriState({ dumpInspectionPassed: true } as RestoreVerificationRunResponseDto)
    ).toBe(true);
    expect(
      mapDumpInspectionTriState({
        dumpInspectionPassed: null,
        pgRestoreListExitCode: 0,
      } as RestoreVerificationRunResponseDto)
    ).toBe(true);
    expect(
      mapDumpInspectionTriState({
        dumpInspectionPassed: null,
        pgRestoreListExitCode: 2,
      } as RestoreVerificationRunResponseDto)
    ).toBe(false);
    expect(
      mapDumpInspectionTriState({
        dumpInspectionPassed: null,
        pgRestoreListExitCode: null,
      } as RestoreVerificationRunResponseDto)
    ).toBe(undefined);
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
