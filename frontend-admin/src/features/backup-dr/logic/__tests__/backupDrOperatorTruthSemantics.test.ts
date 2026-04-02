/**
 * Operatör görünür anlamlar: yanlış yeşil / kanıt birleştirme regresyonlarına karşı.
 */

import { describe, expect, it } from 'vitest';
import type { BackupArtifactResponseDto } from '@/api/generated/model';
import {
  BackupRunResponseDtoStatus,
  BackupVerificationResponseDtoStatus,
  RestoreVerificationRunResponseDtoStatus,
} from '@/api/generated/model';
import {
  automatedRestoreCapabilityFromStatus,
  buildBackupOperatorTruthModel,
  deriveRecoverabilityTruth,
  deriveRunTruth,
} from '@/features/backup-dr/logic/backupDrOperatorTruthModel';
import {
  computeEffectiveRestoreReadinessLevel,
  healthStatisticValueStyle,
  mapArtifactsToExternalCopyVariant,
  mapBackupRunStatusAntdColor,
  mapRestoreVerificationStatusAntdColor,
  restoreReadinessStatisticValueStyle,
} from '@/features/backup-dr/logic/backupDrMappers';
import { summarizeExternalCopyForOperator } from '@/features/backup-dr/logic/backupDrOperatorTruth';
import {
  SERVER_PIPELINE_PROJECTION_VERSION,
  resolveBackupPipelineStepsForUi,
} from '@/features/backup-dr/logic/backupPipelineDerived';

const t = (k: string) => k;

const eightPipelineSteps = [
  { key: 'queued', status: 'success' },
  { key: 'workerRunning', status: 'success' },
  { key: 'dumpComplete', status: 'success' },
  { key: 'artifactCreated', status: 'success' },
  { key: 'artifactVerification', status: 'success' },
  { key: 'manifestCreated', status: 'success' },
  { key: 'externalCopy', status: 'not_required' },
  { key: 'externalChecksum', status: 'not_required' },
] as const;

describe('semantic colors — backup run must not use success/green for plain Succeeded', () => {
  it('table tag color for API success (3) is blue, never success', () => {
    expect(mapBackupRunStatusAntdColor(BackupRunResponseDtoStatus.NUMBER_3)).toBe('blue');
    expect(mapBackupRunStatusAntdColor(BackupRunResponseDtoStatus.NUMBER_3)).not.toBe('success');
  });
});

describe('semantic colors — restore drill finished (2) is cyan, not green success', () => {
  it('does not use Ant Design success green for completed drill', () => {
    const c = mapRestoreVerificationStatusAntdColor(RestoreVerificationRunResponseDtoStatus.NUMBER_2);
    expect(c).toBe('cyan');
    expect(c).not.toBe('success');
  });
});

describe('summary statistics — configuration/readiness healthy use non-green tone', () => {
  it('uses blue for both healthy summary signals (avoid screenshot all-green comfort)', () => {
    const h = healthStatisticValueStyle('healthy');
    const r = restoreReadinessStatisticValueStyle('healthy');
    expect(h?.color).toBe('#1677ff');
    expect(r?.color).toBe('#1677ff');
  });
});

describe('computeEffectiveRestoreReadinessLevel — frontend cap / downgrade', () => {
  const cases: {
    name: string;
    params: Parameters<typeof computeEffectiveRestoreReadinessLevel>[0];
    expected: string | undefined | null;
  }[] = [
    {
      name: 'downgrades healthy to degraded when latest success is simulated',
      params: {
        apiLevel: 'healthy',
        realPostgreSqlLogicalDumpConfiguredHealth: true,
        realPostgreSqlLogicalDumpConfiguredRecoverability: true,
        latestBackupStatus: BackupRunResponseDtoStatus.NUMBER_3,
        isLatestRunSimulatedExecution: true,
        latestAdapterKind: 'PgDump',
      },
      expected: 'degraded',
    },
    {
      name: 'downgrades healthy to degraded when no real pg_dump in health',
      params: {
        apiLevel: 'healthy',
        realPostgreSqlLogicalDumpConfiguredHealth: false,
        realPostgreSqlLogicalDumpConfiguredRecoverability: true,
        latestBackupStatus: BackupRunResponseDtoStatus.NUMBER_3,
        isLatestRunSimulatedExecution: false,
        latestAdapterKind: 'PgDump',
      },
      expected: 'degraded',
    },
    {
      name: 'passes through healthy when real dump and not simulated',
      params: {
        apiLevel: 'healthy',
        realPostgreSqlLogicalDumpConfiguredHealth: true,
        realPostgreSqlLogicalDumpConfiguredRecoverability: true,
        latestBackupStatus: BackupRunResponseDtoStatus.NUMBER_3,
        isLatestRunSimulatedExecution: false,
        latestAdapterKind: 'PgDump',
      },
      expected: 'healthy',
    },
  ];

  it.each(cases)('$name', ({ params, expected }) => {
    expect(computeEffectiveRestoreReadinessLevel(params)).toBe(expected);
  });
});

describe('deriveRunTruth — succeeded but not recoverable', () => {
  it('technical success true but recoverabilityNotProven when proof gaps / stub', () => {
    const r = deriveRunTruth(
      { status: BackupRunResponseDtoStatus.NUMBER_3, id: 'x', adapterKind: 'Fake' } as never,
      null,
      { realPostgreSqlLogicalDumpConfigured: true } as never,
      {
        realPostgreSqlLogicalDumpConfigured: true,
        lastSuccessfulBackupAt: null,
        lastSuccessfulArtifactVerificationAt: 'a',
        lastSuccessfulRestoreProofAt: 'a',
      } as never,
    );
    expect(r.technicalSuccess).toBe(true);
    expect(r.recoverabilityNotProven).toBe(true);
  });
});

describe('deriveRecoverabilityTruth — conflation guard', () => {
  it('recoverabilityNotProven when any proof timestamp missing', () => {
    const x = deriveRecoverabilityTruth({
      lastSuccessfulBackupAt: '2026-01-01',
      lastSuccessfulArtifactVerificationAt: null,
      lastSuccessfulRestoreProofAt: '2026-01-01',
    } as never);
    expect(x.hasProofGaps).toBe(true);
    expect(x.recoverabilityNotProven).toBe(true);
  });
});

describe('buildBackupOperatorTruthModel — combined scenarios', () => {
  it('simulated successful run: banner info (expected stub); run.simulatedEvidence true', () => {
    const m = buildBackupOperatorTruthModel({
      t,
      health: { realPostgreSqlLogicalDumpConfigured: true } as never,
      healthLv: 'healthy',
      restoreReady: { level: 'healthy', workerEnabled: true } as never,
      restoreLv: 'healthy',
      latest: { status: 3, adapterKind: 'Fake', id: 'r1' } as never,
      detailForPipeline: null,
      verification: undefined,
      restoreLatest: undefined,
      recoverabilitySummary: {
        realPostgreSqlLogicalDumpConfigured: true,
        lastSuccessfulBackupAt: 'a',
        lastSuccessfulArtifactVerificationAt: 'a',
        lastSuccessfulRestoreProofAt: 'a',
      } as never,
      restoreCapability: { isAutomatedRestoreAvailable: true },
      externalCopyVariant: 'staging',
    });
    expect(m.run.simulatedEvidence).toBe(true);
    expect(m.run.recoverabilityNotProven).toBe(true);
    expect(m.banner.info.some((w) => w.includes('latestRunSimulatedNotProduction'))).toBe(true);
  });

  it('artifact verification passed but restore drill failed — drill failure is critical, verification not conflated', () => {
    const m = buildBackupOperatorTruthModel({
      t,
      health: { realPostgreSqlLogicalDumpConfigured: true } as never,
      healthLv: 'healthy',
      restoreReady: { level: 'healthy', workerEnabled: true } as never,
      restoreLv: 'healthy',
      latest: { status: BackupRunResponseDtoStatus.NUMBER_3, id: 'b1', adapterKind: 'PgDump' } as never,
      detailForPipeline: null,
      verification: {
        status: BackupVerificationResponseDtoStatus.NUMBER_1,
        backupRunId: 'b1',
        failureReason: null,
      } as never,
      restoreLatest: {
        status: RestoreVerificationRunResponseDtoStatus.NUMBER_3,
        failureCode: 'E_DRILL',
        failureDetail: 'boom',
      } as never,
      recoverabilitySummary: {
        realPostgreSqlLogicalDumpConfigured: true,
        lastSuccessfulBackupAt: '2026-01-01',
        lastSuccessfulArtifactVerificationAt: '2026-01-01',
        lastSuccessfulRestoreProofAt: '2026-01-01',
      } as never,
      restoreCapability: undefined,
      externalCopyVariant: 'unknown',
    });
    expect(m.restore.latestDrillFailed).toBe(true);
    expect(m.banner.critical.some((c) => c.includes('restoreVerification.drillFailed'))).toBe(true);
    expect(m.banner.critical.some((c) => c.includes('artifactVerification.failed'))).toBe(false);
    expect(m.operatorValidity?.titleKey).toBe('backupDr.operatorValidity.latestDrillFailedTitle');
  });

  it('readiness capped: effective differs from API when policy applies', () => {
    const m = buildBackupOperatorTruthModel({
      t,
      health: { realPostgreSqlLogicalDumpConfigured: true } as never,
      healthLv: 'healthy',
      restoreReady: { level: 'healthy', workerEnabled: true } as never,
      restoreLv: 'healthy',
      latest: { status: 3, adapterKind: 'Fake', id: 'r' } as never,
      detailForPipeline: null,
      verification: undefined,
      restoreLatest: undefined,
      recoverabilitySummary: {
        realPostgreSqlLogicalDumpConfigured: true,
        lastSuccessfulBackupAt: 'a',
        lastSuccessfulArtifactVerificationAt: 'a',
        lastSuccessfulRestoreProofAt: 'a',
      } as never,
      restoreCapability: { isAutomatedRestoreAvailable: undefined },
      externalCopyVariant: 'unknown',
    });
    expect(m.restore.readinessCapped).toBe(true);
    expect(m.restore.effectiveReadinessLevel).toBe('degraded');
    expect(m.restore.apiReadinessLevel).toBe('healthy');
  });

  it('backend automated-restore flag: label is reported-enabled key, not operational proof', () => {
    const cap = automatedRestoreCapabilityFromStatus({ isAutomatedRestoreAvailable: true } as never);
    expect(cap.labelKey).toBe('backupDr.restoreCapability.reportedEnabled');
    expect(cap.raw).toBe(true);

    const m = buildBackupOperatorTruthModel({
      t,
      health: undefined,
      healthLv: '',
      restoreReady: undefined,
      restoreLv: '',
      latest: undefined,
      detailForPipeline: null,
      verification: undefined,
      restoreLatest: undefined,
      recoverabilitySummary: undefined,
      restoreCapability: { isAutomatedRestoreAvailable: true },
      externalCopyVariant: 'unknown',
    });
    expect(m.restore.backendReportedCapability.labelKey).toBe('backupDr.restoreCapability.reportedEnabled');
    expect(m.restore.policyUnknown).toBe(false);
  });
});

describe('mapArtifactsToExternalCopyVariant — mixed external archive', () => {
  it('returns mixed when lifecycle states disagree (e.g. one external failed)', () => {
    const artifacts = [
      { lifecycleState: 2 },
      { lifecycleState: 3 },
    ] as BackupArtifactResponseDto[];
    expect(mapArtifactsToExternalCopyVariant(artifacts)).toBe('mixed');
  });

  it('warn surface: mixed maps to mixed i18n via operator model banner', () => {
    const m = buildBackupOperatorTruthModel({
      t,
      health: undefined,
      healthLv: '',
      restoreReady: undefined,
      restoreLv: '',
      latest: undefined,
      detailForPipeline: null,
      verification: undefined,
      restoreLatest: undefined,
      recoverabilitySummary: undefined,
      restoreCapability: undefined,
      externalCopyVariant: 'mixed',
    });
    expect(m.banner.warn).toContain('backupDr.banner.externalArchiveDegraded');
  });
});

describe('resolveBackupPipelineStepsForUi — missing / unsupported projection (no false “official” pipeline)', () => {
  it('blocked with empty steps when no valid snapshot and client fallback off', () => {
    const r = resolveBackupPipelineStepsForUi({ id: 'a', status: 3 } as never, { id: 'a', status: 3 } as never, {}, {
      allowClientFallback: false,
    });
    expect(r.source).toBe('client_fallback_blocked');
    expect(r.steps).toHaveLength(0);
  });

  it('does not use server_projection when projectionVersion is unsupported', () => {
    const r = resolveBackupPipelineStepsForUi(
      { id: 'a' } as never,
      {
        id: 'a',
        pipeline: { projectionVersion: '2099-01-01', steps: [...eightPipelineSteps] },
      } as never,
      {},
      { allowClientFallback: false },
    );
    expect(r.source).toBe('client_fallback_blocked');
    expect(r.projectionVersionMismatch).toBe(true);
    expect(r.steps).toHaveLength(0);
  });

  it('uses server_projection when version matches UI-supported constant', () => {
    const r = resolveBackupPipelineStepsForUi(
      { id: 'a' } as never,
      {
        id: 'a',
        pipeline: { projectionVersion: SERVER_PIPELINE_PROJECTION_VERSION, steps: [...eightPipelineSteps] },
      } as never,
      {},
      { allowClientFallback: false },
    );
    expect(r.source).toBe('server_projection');
    expect(r.steps).toHaveLength(8);
  });
});

describe('summarizeExternalCopyForOperator', () => {
  it('mixed lifecycle maps to mixed text key — metadata class, not off-site proof', () => {
    const s = summarizeExternalCopyForOperator([{ lifecycleState: 2 }, { lifecycleState: 3 }] as never);
    expect(s.variant).toBe('mixed');
    expect(s.textKey).toBe('backupDr.externalCopy.mixed');
  });
});
