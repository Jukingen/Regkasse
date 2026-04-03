import { describe, expect, it } from 'vitest';
import {
  buildBackupOperatorTruthModel,
  deriveArtifactTruth,
  deriveRunTruth,
  tagColorForConfigurationHealthUiKind,
} from '@/features/backup-dr/logic/backupDrOperatorTruthModel';
import {
  configurationHealthSummaryI18nKey,
  mapConfigurationHealthLevel,
} from '@/features/backup-dr/logic/backupDrMappers';
import { BackupRunResponseDtoStatus, RestoreVerificationRunResponseDtoStatus } from '@/api/generated/model';
import { FAKE_ADAPTER_STUB_NOT_PG_RESTORE_FORMAT } from '@/features/backup-dr/logic/restoreVerificationFailurePresentation';

describe('deriveRunTruth', () => {
  it('marks technicalSuccess and simulatedEvidence for Fake adapter on success', () => {
    const r = deriveRunTruth(
      { status: BackupRunResponseDtoStatus.NUMBER_3, id: 'a', adapterKind: 'Fake' } as never,
      null,
      { realPostgreSqlLogicalDumpConfigured: true } as never,
      { realPostgreSqlLogicalDumpConfigured: true } as never,
    );
    expect(r.technicalSuccess).toBe(true);
    expect(r.simulatedEvidence).toBe(true);
    expect(r.simulatedEvidenceSource).toBe('adapter_inference');
    expect(r.recoverabilityNotProven).toBe(true);
    expect(r.dataPlane).toBe('simulated_or_fake');
    expect(r.realPostgreSqlLogicalDumpConfigured).toBe(true);
  });

  it('recoverabilityNotProven when pg_dump not configured', () => {
    const r = deriveRunTruth(
      { status: BackupRunResponseDtoStatus.NUMBER_3, id: 'a', adapterKind: 'PgDump' } as never,
      { isSimulatedExecution: false } as never,
      { realPostgreSqlLogicalDumpConfigured: false } as never,
      { realPostgreSqlLogicalDumpConfigured: false } as never,
    );
    expect(r.simulatedEvidence).toBe(false);
    expect(r.recoverabilityNotProven).toBe(true);
    expect(r.dataPlane).toBe('config_no_real_pg_dump');
    expect(r.realPostgreSqlLogicalDumpConfigured).toBe(false);
  });
});

describe('deriveArtifactTruth', () => {
  const t = (k: string) => k;

  it('matches latest run when backupRunId equals latest id', () => {
    const a = deriveArtifactTruth({ backupRunId: 'run-1', status: 1 } as never, 'run-1', 'staging', t);
    expect(a.globalVerificationScope).toBe('matches_latest_run');
    expect(a.externalCopyDisplayText).toBe('backupDr.externalCopy.stagingOnly');
  });

  it('mismatch when ids differ', () => {
    const a = deriveArtifactTruth({ backupRunId: 'old' } as never, 'new', 'unknown', t);
    expect(a.globalVerificationScope).toBe('mismatch');
  });
});

describe('tagColorForConfigurationHealthUiKind', () => {
  it('unknown is not green', () => {
    expect(tagColorForConfigurationHealthUiKind('unknown')).toBe('default');
  });
  it('healthy is green', () => {
    expect(tagColorForConfigurationHealthUiKind('healthy')).toBe('green');
  });
});

describe('buildBackupOperatorTruthModel', () => {
  const t = (k: string) => k;

  it('exposes restore.policyUnknown when capability flag missing', () => {
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
      restoreCapability: {},
      externalCopyVariant: 'unknown',
    });
    expect(m.restore.policyUnknown).toBe(true);
    expect(m.restore.backendReportedCapability.raw).toBeUndefined();
  });

  it('merges banner and alerts from same rules', () => {
    const m = buildBackupOperatorTruthModel({
      t,
      health: undefined,
      healthLv: '',
      restoreReady: undefined,
      restoreLv: '',
      latest: { status: 3, adapterKind: 'Fake' } as never,
      detailForPipeline: null,
      verification: undefined,
      restoreLatest: undefined,
      recoverabilitySummary: undefined,
      restoreCapability: undefined,
      externalCopyVariant: 'unknown',
    });
    expect(m.banner.info.some((x) => x.includes('latestRunSimulatedNotProduction'))).toBe(true);
  });

  it('does not repeat simulated banner line when no pg_dump is already explained (dev/fake)', () => {
    const m = buildBackupOperatorTruthModel({
      t,
      health: { realPostgreSqlLogicalDumpConfigured: false, readinessNarrative: 'fake adapter' } as never,
      healthLv: 'healthy',
      restoreReady: { level: 'healthy', workerEnabled: true } as never,
      restoreLv: 'healthy',
      latest: { status: 3, adapterKind: 'Fake', id: 'r1' } as never,
      detailForPipeline: null,
      verification: undefined,
      restoreLatest: undefined,
      recoverabilitySummary: undefined,
      restoreCapability: undefined,
      externalCopyVariant: 'unknown',
    });
    expect(m.banner.info.some((x) => x.includes('latestRunSimulatedNotProduction'))).toBe(false);
    expect(m.banner.warn.some((x) => x.includes('noRealPostgreSqlBackup'))).toBe(true);
  });

  it('omits health/restore API issue rows when omitDedicatedSectionIssueDuplicates (dashboard cards list them)', () => {
    const withDupes = buildBackupOperatorTruthModel({
      t,
      health: { issues: ['disk full', 'path invalid'] } as never,
      healthLv: 'degraded',
      restoreReady: { issues: ['worker slow'] } as never,
      restoreLv: 'degraded',
      latest: undefined,
      detailForPipeline: null,
      verification: undefined,
      restoreLatest: undefined,
      recoverabilitySummary: undefined,
      restoreCapability: undefined,
      externalCopyVariant: 'unknown',
    });
    expect(withDupes.alerts.some((a) => a.text === 'disk full')).toBe(true);

    const deduped = buildBackupOperatorTruthModel({
      t,
      health: { issues: ['disk full', 'path invalid'] } as never,
      healthLv: 'degraded',
      restoreReady: { issues: ['worker slow'] } as never,
      restoreLv: 'degraded',
      latest: undefined,
      detailForPipeline: null,
      verification: undefined,
      restoreLatest: undefined,
      recoverabilitySummary: undefined,
      restoreCapability: undefined,
      externalCopyVariant: 'unknown',
      omitDedicatedSectionIssueDuplicates: true,
    });
    expect(deduped.alerts.some((a) => a.text === 'disk full')).toBe(false);
    expect(deduped.alerts.some((a) => a.text === 'worker slow')).toBe(false);
  });

  it('omits banner-mirrored rows from alerts card (drill failure stays in banner + dedicated cards)', () => {
    const m = buildBackupOperatorTruthModel({
      t,
      health: undefined,
      healthLv: '',
      restoreReady: undefined,
      restoreLv: '',
      latest: undefined,
      detailForPipeline: null,
      verification: undefined,
      restoreLatest: {
        status: 3,
        failureCode: 'E',
        failureDetail: 'drill boom',
      } as never,
      recoverabilitySummary: undefined,
      restoreCapability: undefined,
      externalCopyVariant: 'unknown',
    });
    expect(m.banner.critical.some((c) => c.includes('restoreVerification.drillFailed'))).toBe(true);
    expect(m.alerts.some((a) => a.text.includes('drillFailedAlert'))).toBe(false);
  });

  it('does not duplicate noRealPostgreSqlBackup in Alerts when banner already states it (redundantWithBanner)', () => {
    const m = buildBackupOperatorTruthModel({
      t,
      health: { realPostgreSqlLogicalDumpConfigured: false, readinessNarrative: 'no pg' } as never,
      healthLv: 'degraded',
      restoreReady: undefined,
      restoreLv: '',
      latest: undefined,
      detailForPipeline: null,
      verification: undefined,
      restoreLatest: undefined,
      recoverabilitySummary: undefined,
      restoreCapability: undefined,
      externalCopyVariant: 'unknown',
    });
    expect(m.banner.warn.some((w) => w.includes('noRealPostgreSqlBackup'))).toBe(true);
    expect(m.alerts.some((a) => a.text.includes('noRealPostgreSqlBackup'))).toBe(false);
  });

  it('does not duplicate artifact verification failure in Alerts when banner already shows it', () => {
    const m = buildBackupOperatorTruthModel({
      t,
      health: undefined,
      healthLv: '',
      restoreReady: undefined,
      restoreLv: '',
      latest: undefined,
      detailForPipeline: null,
      verification: { status: 2, failureReason: 'checksum mismatch' } as never,
      restoreLatest: undefined,
      recoverabilitySummary: undefined,
      restoreCapability: undefined,
      externalCopyVariant: 'unknown',
    });
    expect(m.banner.warn.some((w) => w.includes('artifactVerification.failed'))).toBe(true);
    expect(m.alerts.some((a) => a.text.includes('artifactVerification.failed'))).toBe(false);
  });

  it('does not duplicate latest run failure line in Alerts when banner already shows it', () => {
    const m = buildBackupOperatorTruthModel({
      t,
      health: undefined,
      healthLv: '',
      restoreReady: undefined,
      restoreLv: '',
      latest: { status: 4, failureCode: 'X', failureDetail: 'boom' } as never,
      detailForPipeline: null,
      verification: undefined,
      restoreLatest: undefined,
      recoverabilitySummary: undefined,
      restoreCapability: undefined,
      externalCopyVariant: 'unknown',
    });
    expect(m.banner.critical.some((c) => c.includes('backupDr.latestRun.failure'))).toBe(true);
    expect(m.alerts.some((a) => a.text.includes('backupDr.latestRun.failure'))).toBe(false);
  });

  it('restore drill PG_RESTORE_LIST_FAILED on fake stub: banner warn (expected), not critical', () => {
    const m = buildBackupOperatorTruthModel({
      t,
      health: undefined,
      healthLv: '',
      restoreReady: undefined,
      restoreLv: '',
      latest: { status: 3, adapterKind: 'Fake', id: 'r1' } as never,
      detailForPipeline: null,
      verification: undefined,
      restoreLatest: {
        status: RestoreVerificationRunResponseDtoStatus.NUMBER_3,
        failureCode: 'PG_RESTORE_LIST_FAILED',
        failureDetail: 'pg_restore failed',
        detailsJson: JSON.stringify({
          pgRestoreListFailureContext: { reason: FAKE_ADAPTER_STUB_NOT_PG_RESTORE_FORMAT },
        }),
      } as never,
      recoverabilitySummary: undefined,
      restoreCapability: undefined,
      externalCopyVariant: 'unknown',
    });
    expect(m.banner.warn.some((w) => w.includes('restoreDrillStubListFailedExpected'))).toBe(true);
    expect(m.banner.critical.some((c) => c.includes('restoreVerification.drillFailed'))).toBe(false);
  });

  it('restore drill PG_RESTORE_LIST_FAILED real pipeline + pg_restore not startable: banner warning, not critical', () => {
    const m = buildBackupOperatorTruthModel({
      t,
      health: {
        effectiveAdapterKind: 'PgDump',
        realPostgreSqlLogicalDumpConfigured: true,
        workerEnabled: true,
      } as never,
      healthLv: 'healthy',
      restoreReady: undefined,
      restoreLv: '',
      latest: { status: 3, adapterKind: 'PgDump', id: 'r1' } as never,
      detailForPipeline: null,
      verification: undefined,
      restoreLatest: {
        status: RestoreVerificationRunResponseDtoStatus.NUMBER_3,
        failureCode: 'PG_RESTORE_LIST_FAILED',
        failureDetail: 'Failed to start pg_restore.',
        pgRestoreListExitCode: -1,
      } as never,
      recoverabilitySummary: undefined,
      restoreCapability: undefined,
      externalCopyVariant: 'unknown',
    });
    expect(m.banner.warn.some((w) => w.includes('restoreDrillRealListFailedPgRestoreUnavailable'))).toBe(true);
    expect(m.banner.critical.some((c) => c.includes('restoreDrillRealListFailed'))).toBe(false);
  });

  it('exposes operatorValidity strip for simulated data plane', () => {
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
      recoverabilitySummary: undefined,
      restoreCapability: undefined,
      externalCopyVariant: 'unknown',
    });
    expect(m.operatorValidity?.titleKey).toBe('backupDr.operatorValidity.stubDataPlaneTitle');
    expect(m.operatorValidity?.severity).toBe('info');
  });

  it('centralizes dashboard-facing labels/lock hints/summary presentation', () => {
    const m = buildBackupOperatorTruthModel({
      t,
      health: {
        effectiveAdapterKind: 'Fake',
        realPostgreSqlLogicalDumpConfigured: false,
        issues: ['distributed lock disabled', 'path missing'],
      } as never,
      healthLv: 'degraded',
      restoreReady: {
        level: 'healthy',
        workerEnabled: true,
        issues: ['advisory lock timeout'],
      } as never,
      restoreLv: 'healthy',
      latest: { id: 'r1', status: 3, adapterKind: 'Fake' } as never,
      detailForPipeline: null,
      verification: undefined,
      restoreLatest: undefined,
      recoverabilitySummary: undefined,
      restoreCapability: undefined,
      externalCopyVariant: 'unknown',
      hasStatusPayload: true,
    });

    expect(m.labels.backupStatus(3)).toBe('3');
    expect(m.labels.restoreStatus(2)).toBe('2');
    expect(m.lockHints.backup).toEqual(['distributed lock disabled']);
    expect(m.lockHints.restore).toEqual(['advisory lock timeout']);
    expect(m.summaryPresentation.summaryBackupFootnoteKey).toBe('backupDr.summary.backupHealthFootnoteFake');
    expect(m.summaryPresentation.showDevRealDumpGuidance).toBe(true);
    expect(m.summaryPresentation.showRealPgDumpOperationalBanner).toBe(false);
    expect(m.manualActionsModeConfirmations.backupTitle).toBe('backupDr.manual.confirmBackupTitle');
    expect(m.truthProvenance.simulatedOperationalSurface.runSimulatedSource).toBe('adapter_inference');
    expect(m.truthProvenance.recoverabilityProofStrength.hasProofGapsFromTimestamps).toBe(m.recoverability.hasProofGaps);
    expect(m.truthProvenance.externalArchiveProofStrength.variantKind).toBe('frontend_inferred');
    expect(m.summaryPresentation.backupHealthSummaryLabelKey).toBe(configurationHealthSummaryI18nKey(undefined));
    expect(m.summaryPresentation.restoreReadinessSummaryLabelKey).toBe(
      configurationHealthSummaryI18nKey(m.restore.effectiveReadinessLevel),
    );
    expect(m.summaryPresentation.backupHealthUiKind).toBe(mapConfigurationHealthLevel(undefined));
    expect(m.summaryPresentation.restoreReadinessUiKind).toBe(
      mapConfigurationHealthLevel(m.restore.effectiveReadinessLevel),
    );
    expect(m.summaryPresentation.configShortSummaryLine).toContain('Fake');
    expect(m.summaryPresentation.configShortSummaryLine).toContain('backupDr.health.realPgDumpNo');
    expect(m.progressRunBanner.recoverabilityNotProvenGlance).toBe(true);
    expect(m.progressRunBanner.latestRestoreDrillFailed).toBe(false);
  });

  it('progressRunBanner mirrors latest restore drill failed from truth model', () => {
    const m = buildBackupOperatorTruthModel({
      t,
      health: undefined,
      healthLv: '',
      restoreReady: undefined,
      restoreLv: '',
      latest: undefined,
      detailForPipeline: null,
      verification: undefined,
      restoreLatest: { status: RestoreVerificationRunResponseDtoStatus.NUMBER_3 } as never,
      recoverabilitySummary: undefined,
      restoreCapability: undefined,
      externalCopyVariant: 'unknown',
    });
    expect(m.progressRunBanner.latestRestoreDrillFailed).toBe(true);
    expect(m.restore.latestDrillFailed).toBe(true);
  });

  it('suppressRestoreDrillFailureInHealthBanner removes drill line from HealthBanner critical without changing restore.latestDrillFailed', () => {
    const base = {
      t,
      health: undefined,
      healthLv: '',
      restoreReady: undefined,
      restoreLv: '',
      latest: undefined,
      detailForPipeline: null,
      verification: undefined,
      restoreLatest: {
        status: RestoreVerificationRunResponseDtoStatus.NUMBER_3,
        failureCode: 'E_TEST',
        failureDetail: 'x',
      } as never,
      recoverabilitySummary: undefined,
      restoreCapability: undefined,
      externalCopyVariant: 'unknown' as const,
    };
    const withDrillInBanner = buildBackupOperatorTruthModel({ ...base, suppressRestoreDrillFailureInHealthBanner: false });
    const suppressed = buildBackupOperatorTruthModel({ ...base, suppressRestoreDrillFailureInHealthBanner: true });
    expect(withDrillInBanner.restore.latestDrillFailed).toBe(true);
    expect(suppressed.restore.latestDrillFailed).toBe(true);
    expect(withDrillInBanner.banner.critical.length).toBeGreaterThan(0);
    expect(suppressed.banner.critical.length).toBe(0);
  });
});
