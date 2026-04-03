/**
 * Regresyon: “teknik başarı ama gerçek DB yedeği yok” senaryosu — operatörün gördüğü yüzeyler kilitlenir.
 * Fake adaptör + simüle çalıştırma asla üretim pg_dump kanıtı olarak sunulmamalı; PgDump gerçek yolunda Fake dil kullanılmamalı.
 * Bileşen + pano entegrasyonu: yalnızca saf yardımcı fonksiyon testi değil.
 */

import React from 'react';
import '@testing-library/jest-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import type { BackupRecoverabilitySummaryResponseDto } from '@/api/generated/model';
import { RestoreVerificationRunResponseDtoStatus } from '@/api/generated/model';
import { FAKE_ADAPTER_STUB_NOT_PG_RESTORE_FORMAT } from '@/features/backup-dr/logic/restoreVerificationFailurePresentation';
import { mapDumpInspectionTriState } from '@/features/backup-dr/logic/backupDrMappers';

vi.mock('@/lib/axios', () => ({
  AXIOS_INSTANCE: { get: vi.fn(), interceptors: { request: { use: vi.fn() }, response: { use: vi.fn() } } },
  customInstance: vi.fn(),
}));

vi.mock('@/features/backup-dr/logic/backupExecutionModeApi', async () => {
  const q = await import('@/features/backup-dr/logic/backupExecutionModeQueryKeys');
  return {
    getGetApiAdminBackupExecutionModeQueryKey: q.getGetApiAdminBackupExecutionModeQueryKey,
    BACKUP_EXECUTION_MODE_API_PATH: q.BACKUP_EXECUTION_MODE_API_PATH,
    getBackupExecutionMode: vi.fn(() =>
      Promise.resolve({
        storedMode: 'InheritFromConfiguration',
        requestedUserFacingMode: 'UseConfigurationDefault',
        configurationDefaultUserFacingMode: 'Fake',
        effectiveUserFacingMode: 'Fake',
        recommendedFallbackUserFacingMode: null,
        adapterKindIfConfigurationDefaultOnly: 'Fake',
        effectiveModeResolutionSummaryEnglish: 'test',
        configurationExecutionAdapterKind: 'Fake',
        effectiveExecutionAdapterKind: 'Fake',
        effectiveModeRunnable: true,
        hypotheticalPgDumpHealthLevel: 'Healthy',
        blockers: [],
        realModeBlockingDiagnostics: [],
        selectableModes: [],
        effectiveConfigurationHealth: {},
      }),
    ),
    putBackupExecutionMode: vi.fn(),
  };
});

import {
  useGetApiAdminBackupRecoverabilitySummary,
  useGetApiAdminBackupRuns,
  useGetApiAdminBackupRunsId,
  useGetApiAdminBackupStatusLatest,
  useGetApiAdminBackupVerificationLatest,
  usePostApiAdminBackupTrigger,
} from '@/api/generated/admin-backup/admin-backup';
import {
  useGetApiAdminRestoreVerificationReadiness,
  useGetApiAdminRestoreVerificationRuns,
  useGetApiAdminRestoreVerificationRunsLatest,
  usePostApiAdminRestoreVerificationTrigger,
} from '@/api/generated/admin-restore-verification/admin-restore-verification';
import { BackupDrDashboard } from '@/features/backup-dr/components/BackupDrDashboard';
import { BackupArtifactsDownloadCard } from '@/features/backup-dr/components/BackupArtifactsDownloadCard';
import { BackupStatusCard } from '@/features/backup-dr/components/BackupStatusCard';
import { RecoverabilitySummaryCard } from '@/features/backup-dr/components/RecoverabilitySummaryCard';
import { RestoreVerificationCard } from '@/features/backup-dr/components/RestoreVerificationCard';
import type { BackupRunResponseDto } from '@/api/generated/model';
import { BackupArtifactResponseDtoArtifactType } from '@/api/generated/model/backupArtifactResponseDtoArtifactType';

const t = (k: string) => k;

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t,
    formatLocale: 'en-US',
  }),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => ({
    user: { permissions: ['settings.manage'] },
  }),
}));

vi.mock('@/features/backup-dr/logic/backupPipelineEnv', () => ({
  isBackupPipelineClientFallbackEnabled: () => false,
}));

vi.mock('@/api/generated/admin-backup/admin-backup', () => ({
  getGetApiAdminBackupRecoverabilitySummaryQueryKey: () => ['/api/admin/backup/recoverability-summary'] as const,
  getGetApiAdminBackupRunsIdQueryKey: (id: string) => [`/api/admin/backup/runs/${id}`] as const,
  getGetApiAdminBackupRunsQueryKey: (params?: { page?: number; pageSize?: number }) =>
    ['/api/admin/backup/runs', ...(params ? [params] : [])] as const,
  getGetApiAdminBackupVerificationLatestQueryKey: () => ['/api/admin/backup/verification/latest'] as const,
  useGetApiAdminBackupStatusLatest: vi.fn(),
  useGetApiAdminBackupRuns: vi.fn(),
  useGetApiAdminBackupVerificationLatest: vi.fn(),
  useGetApiAdminBackupRecoverabilitySummary: vi.fn(),
  useGetApiAdminBackupRunsId: vi.fn(),
  usePostApiAdminBackupTrigger: vi.fn(),
}));

vi.mock('@/api/generated/admin-restore-verification/admin-restore-verification', () => ({
  getGetApiAdminRestoreVerificationReadinessQueryKey: () => ['/api/admin/restore-verification/readiness'] as const,
  getGetApiAdminRestoreVerificationRunsLatestQueryKey: () => ['/api/admin/restore-verification/runs/latest'] as const,
  getGetApiAdminRestoreVerificationRunsQueryKey: (params?: { page?: number; pageSize?: number }) =>
    ['/api/admin/restore-verification/runs', ...(params ? [params] : [])] as const,
  useGetApiAdminRestoreVerificationReadiness: vi.fn(),
  useGetApiAdminRestoreVerificationRuns: vi.fn(),
  useGetApiAdminRestoreVerificationRunsLatest: vi.fn(),
  usePostApiAdminRestoreVerificationTrigger: vi.fn(),
}));

function wrap(ui: React.ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return <QueryClientProvider client={client}>{ui}</QueryClientProvider>;
}

beforeAll(() => {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    configurable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

const formatDt = (iso: string | undefined | null) => String(iso ?? '');

function mockPostTriggers() {
  vi.mocked(usePostApiAdminBackupTrigger).mockReturnValue({ mutate: vi.fn(), isPending: false } as never);
  vi.mocked(usePostApiAdminRestoreVerificationTrigger).mockReturnValue({ mutate: vi.fn(), isPending: false } as never);
}

function baseFakeStatusLatest(over: Record<string, unknown> = {}) {
  return {
    data: {
      latestRun: {
        id: 'run-fake-1',
        status: 3,
        adapterKind: 'Fake',
        requestedAt: '2026-01-10T10:00:00Z',
        startedAt: '2026-01-10T10:00:01Z',
        completedAt: '2026-01-10T10:05:00Z',
      },
      configurationHealth: {
        level: 'healthy',
        workerEnabled: true,
        effectiveAdapterKind: 'Fake',
        realPostgreSqlLogicalDumpConfigured: false,
        issues: [],
      },
      artifactPipelinePolicy: {},
      restore: { isAutomatedRestoreAvailable: false, notes: '' },
      averageSucceededBackupDurationSeconds: null,
      averageSucceededBackupDurationSampleCount: null,
      ...over,
    },
    isLoading: false,
    isError: false,
    isFetching: false,
  };
}

function baseRecoverabilityFake(over: Partial<BackupRecoverabilitySummaryResponseDto> = {}) {
  return {
    data: {
      lastSuccessfulBackupAt: '2026-01-10T10:05:00Z',
      lastSuccessfulArtifactVerificationAt: '2026-01-10T10:06:00Z',
      lastSuccessfulRestoreProofAt: '2026-01-01T00:00:00Z',
      lastSuccessfulBackupRunId: 'run-fake-1',
      lastSuccessfulBackupRunIsSimulatedExecution: true,
      lastSuccessfulRestoreProofRunId: 'old-drill-1',
      realPostgreSqlLogicalDumpConfigured: false,
      latestRunAt: '2026-01-10T10:00:00Z',
      latestRunStatus: 3,
      latestRestoreRunAt: '2026-01-10T11:00:00Z',
      latestRestoreRunStatus: 3,
      backupProofAgeSeconds: 100,
      restoreProofAgeSeconds: 200,
      backupExecutionReality: 'Simulated',
      backupReadinessLevel: 'healthy',
      backupReadinessNarrative: '',
      ...over,
    },
    isLoading: false,
    isError: false,
  };
}

describe('RestoreVerificationCard — PG_RESTORE_LIST_FAILED', () => {
  const baseRun = {
    id: 'rv-1',
    status: RestoreVerificationRunResponseDtoStatus.NUMBER_3,
    failureCode: 'PG_RESTORE_LIST_FAILED',
    failureDetail: 'pg_restore: error',
    completedAt: '2026-01-10T12:00:00Z',
    dumpInspectionPassed: false,
    restoreAttemptExecuted: false,
  };

  it('Fake/stub pipeline: expected failure copy — not generic “action required” mystery', () => {
    render(
      <RestoreVerificationCard
        run={{
          ...baseRun,
          detailsJson: JSON.stringify({
            pgRestoreListFailureContext: { reason: FAKE_ADAPTER_STUB_NOT_PG_RESTORE_FORMAT },
          }),
        }}
        formatDt={formatDt}
        formatLocale="en-US"
        restoreStatusTagColor={() => 'red'}
        restoreStatusLabel={(s) => `st-${s}`}
        dumpInspectionTriState={mapDumpInspectionTriState}
        isSimulatedBackupPipeline
        t={t}
      />,
    );

    expect(screen.getByText('backupDr.restoreVerification.fakePipeline.sectionContext')).toBeInTheDocument();
    expect(screen.getByText('backupDr.restoreVerification.fakePipeline.drillOutcomeTitle')).toBeInTheDocument();
    expect(screen.getByText('backupDr.restoreVerification.fakePipeline.pgRestoreListExplainer')).toBeInTheDocument();
    expect(screen.getByText('backupDr.triState.dumpInspectionNotApplicableStub')).toBeInTheDocument();
    expect(screen.getByText('backupDr.restoreStatus.drillStubExpected')).toBeInTheDocument();
    expect(screen.queryByText('backupDr.restoreVerification.drillFailedProminent')).not.toBeInTheDocument();
  });

  it('non-simulated pipeline: pg_restore rejected file — real archive risk copy (not stub explainer)', () => {
    render(
      <RestoreVerificationCard
        run={{
          ...baseRun,
          detailsJson: '{}',
          pgRestoreListExitCode: 1,
        }}
        formatDt={formatDt}
        formatLocale="en-US"
        restoreStatusTagColor={() => 'red'}
        restoreStatusLabel={(s) => `st-${s}`}
        dumpInspectionTriState={mapDumpInspectionTriState}
        isSimulatedBackupPipeline={false}
        t={t}
      />,
    );

    expect(screen.getByText('backupDr.restoreVerification.realPipeline.listFailedFormatRejectedTitle')).toBeInTheDocument();
    expect(screen.queryByText('backupDr.restoreVerification.drillFailedProminent')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.restoreVerification.fakePipeline.drillOutcomeTitle')).not.toBeInTheDocument();
  });
});

describe('BackupArtifactsDownloadCard — stub meaning on rows', () => {
  /** Çeviri anahtarı === metin olduğunda kart “unknown” gösterir; stub satırı için anahtardan farklı etiket ver. */
  const tArtifact = (k: string, o?: Record<string, string | number>) => {
    if (k === 'backupDr.download.types.logicalDumpStub') return '[stub-logical-row]';
    if (k === 'backupDr.latestRun.bytesB' && o?.n) return `bytes-${o.n}`;
    return o ? `${k}` : k;
  };

  it('shows stub zone + per-row content expectation (not production dump)', () => {
    render(
      <BackupArtifactsDownloadCard
        variant="latest_success"
        runId="run-fake"
        artifacts={[
          {
            id: 'a-dump',
            artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
            isFilePresentForDownload: true,
            byteSize: 42,
          },
        ]}
        canManage
        isSimulatedExecution
        runAdapterKind="Fake"
        realPostgreSqlLogicalDumpConfigured={false}
        simulatedOperationalMode
        t={tArtifact}
      />,
    );

    expect(screen.getByText('backupDr.download.stubZoneAlertTitle')).toBeInTheDocument();
    expect(screen.getByText('[stub-logical-row]')).toBeInTheDocument();
    expect(screen.getByText('backupDr.download.recoverabilityUse.short.not_dr_evidence_simulated')).toBeInTheDocument();
    expect(screen.getByText('backupDr.download.contentExpectSummary.stubLogicalDumpFakeAdapter')).toBeInTheDocument();
    expect(screen.queryByText('backupDr.download.types.logicalDumpOperational')).not.toBeInTheDocument();
  });
});

describe('RecoverabilitySummaryCard — real pg_dump path visible', () => {
  const fullSummary: BackupRecoverabilitySummaryResponseDto = {
    lastSuccessfulBackupAt: '2026-01-01T00:00:00Z',
    lastSuccessfulArtifactVerificationAt: '2026-01-01T00:00:00Z',
    lastSuccessfulRestoreProofAt: '2026-01-01T00:00:00Z',
    realPostgreSqlLogicalDumpConfigured: false,
    latestRunAt: '2026-01-02T00:00:00Z',
    latestRunStatus: 3,
    latestRestoreRunAt: '2026-01-02T00:00:00Z',
    latestRestoreRunStatus: 3,
    backupProofAgeSeconds: 1,
    restoreProofAgeSeconds: 1,
  };

  it('shows lack of real PostgreSQL logical dump in execution profile block', () => {
    render(
      <RecoverabilitySummaryCard
        summary={fullSummary}
        loading={false}
        formatDt={formatDt}
        formatLocale="en-US"
        backupStatusLabel={(s) => `b-${s}`}
        restoreStatusLabel={(s) => `r-${s}`}
        simulatedOperationalMode={false}
        t={t}
      />,
    );

    const card = screen.getByText('backupDr.recoverability.title').closest('.ant-card');
    expect(card).toBeTruthy();
    expect(within(card as HTMLElement).getByText('backupDr.recoverability.realPgDumpConfigured')).toBeInTheDocument();
    expect(within(card as HTMLElement).getByText('common.buttons.no')).toBeInTheDocument();
  });
});

describe('BackupStatusCard — technical success vs simulated vs real status label', () => {
  const latestFake: BackupRunResponseDto = {
    id: 'run-1',
    status: 3,
    adapterKind: 'Fake',
    requestedAt: '2026-01-01T00:00:00Z',
    startedAt: '2026-01-01T00:00:01Z',
    completedAt: '2026-01-01T00:01:00Z',
  } as BackupRunResponseDto;

  it('Fake + success: simulated technical success — not a “Succeeded” production backup label', () => {
    render(
      <BackupStatusCard
        latest={latestFake}
        detail={{ isSimulatedExecution: true, adapterKind: 'Fake', artifacts: [] } as never}
        policy={undefined}
        loadingDetail={false}
        detailError={false}
        formatDt={formatDt}
        formatLocale="en-US"
        backupStatusTagColor={() => 'blue'}
        backupStatusLabel={(s) => `backupDr.backupStatus.${s}`}
        operatorRunTruth={{ technicalSuccess: true, simulatedEvidence: true }}
        simulatedOperationalMode
        omitFakeOperationalNotice
        t={t}
      />,
    );

    expect(screen.getByText('backupDr.backupStatus.simulatedSuccess')).toBeInTheDocument();
    expect(screen.getByText('backupDr.latestRun.simulatedBadge')).toBeInTheDocument();
    expect(screen.queryByText('backupDr.backupStatus.3')).not.toBeInTheDocument();
  });

  it('PgDump + success: ordinary succeeded label — no simulated success wording', () => {
    const latestPg: BackupRunResponseDto = {
      ...latestFake,
      adapterKind: 'PgDump',
    };
    render(
      <BackupStatusCard
        latest={latestPg}
        detail={{ isSimulatedExecution: false, adapterKind: 'PgDump', artifacts: [] } as never}
        policy={undefined}
        loadingDetail={false}
        detailError={false}
        formatDt={formatDt}
        formatLocale="en-US"
        backupStatusTagColor={() => 'blue'}
        backupStatusLabel={(s) => `backupDr.backupStatus.${s}`}
        operatorRunTruth={{ technicalSuccess: true, simulatedEvidence: false }}
        simulatedOperationalMode={false}
        t={t}
      />,
    );

    expect(screen.getByText('backupDr.backupStatus.3')).toBeInTheDocument();
    expect(screen.queryByText('backupDr.backupStatus.simulatedSuccess')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.latestRun.simulatedBadge')).not.toBeInTheDocument();
  });
});

describe('BackupDrDashboard — integration: Fake vs PgDump wording separation', () => {
  beforeEach(() => {
    mockPostTriggers();
    vi.mocked(useGetApiAdminBackupRuns).mockReturnValue({
      data: { items: [] },
      isLoading: false,
      isError: false,
      isFetching: false,
    } as never);
    vi.mocked(useGetApiAdminBackupVerificationLatest).mockReturnValue({
      data: { status: 1, backupRunId: 'run-fake-1', completedAt: '2026-01-10T10:06:00Z', verifierSource: 'test' },
      isLoading: false,
      isError: false,
    } as never);
    vi.mocked(useGetApiAdminBackupRunsId).mockReturnValue({
      data: {
        id: 'run-fake-1',
        status: 3,
        isSimulatedExecution: true,
        adapterKind: 'Fake',
        artifacts: [
          {
            id: 'art-1',
            artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
            isFilePresentForDownload: true,
            byteSize: 40,
          },
        ],
      },
      isLoading: false,
      isFetching: false,
      isError: false,
    } as never);
    vi.mocked(useGetApiAdminRestoreVerificationReadiness).mockReturnValue({
      data: {
        level: 'healthy',
        workerEnabled: true,
        orchestratorDistributedLockEnabled: true,
        issues: [],
        scopeDisclaimer: '',
      },
      isLoading: false,
      isError: false,
    } as never);
    vi.mocked(useGetApiAdminRestoreVerificationRuns).mockReturnValue({
      data: { items: [] },
      isLoading: false,
      isError: false,
      isFetching: false,
    } as never);
  });

  it('Fake + no real pg_dump: never implies production backup; shows stub validity + simulated success + pg_dump no', () => {
    vi.mocked(useGetApiAdminBackupStatusLatest).mockReturnValue(baseFakeStatusLatest() as never);
    vi.mocked(useGetApiAdminBackupRecoverabilitySummary).mockReturnValue(baseRecoverabilityFake() as never);
    vi.mocked(useGetApiAdminRestoreVerificationRunsLatest).mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
    } as never);

    render(wrap(<BackupDrDashboard />));

    expect(screen.getByText('backupDr.fakeMode.bannerTitle')).toBeInTheDocument();
    expect(screen.getByText('backupDr.devRealPgDump.title')).toBeInTheDocument();
    expect(screen.queryByText('backupDr.realDumpMode.bannerTitle')).not.toBeInTheDocument();
    expect(screen.getByText('backupDr.postureSummary.simulatedModeTag')).toBeInTheDocument();
    expect(screen.getByText('backupDr.confidenceDashboard.strip.stubTitle')).toBeInTheDocument();
    expect(screen.getByText(/backupDr\.health\.realPgDumpNo/)).toBeInTheDocument();
    expect(screen.getByText('backupDr.summary.backupHealthFootnoteFake')).toBeInTheDocument();
    expect(screen.getByText('backupDr.backupStatus.simulatedSuccess')).toBeInTheDocument();
    expect(screen.getByText('backupDr.progress.finishedSimulatedOkDetail')).toBeInTheDocument();
    expect(screen.getByText('backupDr.download.titleLatestSuccessFake')).toBeInTheDocument();
    expect(screen.queryByText('backupDr.download.titleLatestSuccess')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.backupStatus.3')).not.toBeInTheDocument();
    expect(screen.getByText('backupDr.download.reality.stub')).toBeInTheDocument();
  });

  it('PgDump + real pg_dump: no Fake banner, no Fake download title, no stub operator strip', () => {
    vi.mocked(useGetApiAdminBackupStatusLatest).mockReturnValue({
      data: {
        latestRun: {
          id: 'run-pg-1',
          status: 3,
          adapterKind: 'PgDump',
          requestedAt: '2026-01-10T10:00:00Z',
          startedAt: '2026-01-10T10:00:01Z',
          completedAt: '2026-01-10T10:05:00Z',
        },
        configurationHealth: {
          level: 'healthy',
          workerEnabled: true,
          effectiveAdapterKind: 'PgDump',
          realPostgreSqlLogicalDumpConfigured: true,
          issues: [],
        },
        artifactPipelinePolicy: {},
        restore: { isAutomatedRestoreAvailable: true, notes: '' },
        averageSucceededBackupDurationSeconds: null,
        averageSucceededBackupDurationSampleCount: null,
      },
      isLoading: false,
      isError: false,
      isFetching: false,
    } as never);

    vi.mocked(useGetApiAdminBackupRecoverabilitySummary).mockReturnValue({
      data: {
        lastSuccessfulBackupAt: '2026-01-10T10:05:00Z',
        lastSuccessfulArtifactVerificationAt: '2026-01-10T10:06:00Z',
        lastSuccessfulRestoreProofAt: '2026-01-10T11:00:00Z',
        lastSuccessfulBackupRunId: 'run-pg-1',
        lastSuccessfulBackupRunIsSimulatedExecution: false,
        lastSuccessfulRestoreProofRunId: 'drill-ok-1',
        realPostgreSqlLogicalDumpConfigured: true,
        latestRunAt: '2026-01-10T10:00:00Z',
        latestRunStatus: 3,
        latestRestoreRunAt: '2026-01-10T11:00:00Z',
        latestRestoreRunStatus: 2,
        backupProofAgeSeconds: 60,
        restoreProofAgeSeconds: 60,
        backupExecutionReality: 'Operational',
        backupReadinessLevel: 'healthy',
        backupReadinessNarrative: '',
      },
      isLoading: false,
      isError: false,
    } as never);

    vi.mocked(useGetApiAdminBackupRunsId).mockReturnValue({
      data: {
        id: 'run-pg-1',
        status: 3,
        isSimulatedExecution: false,
        adapterKind: 'PgDump',
        artifacts: [
          {
            id: 'art-pg',
            artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
            isFilePresentForDownload: true,
            byteSize: 500_000,
          },
        ],
      },
      isLoading: false,
      isFetching: false,
      isError: false,
    } as never);

    vi.mocked(useGetApiAdminRestoreVerificationRunsLatest).mockReturnValue({
      data: {
        id: 'drill-ok-1',
        status: RestoreVerificationRunResponseDtoStatus.NUMBER_2,
        completedAt: '2026-01-10T11:00:00Z',
      },
      isLoading: false,
      isError: false,
    } as never);

    render(wrap(<BackupDrDashboard />));

    expect(screen.queryByText('backupDr.fakeMode.bannerTitle')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.summary.backupHealthFootnoteFake')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.download.titleLatestSuccessFake')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.operatorValidity.stubDataPlaneTitle')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.devRealPgDump.title')).not.toBeInTheDocument();
    expect(screen.getByText('backupDr.realDumpMode.bannerTitle')).toBeInTheDocument();
    expect(screen.getByText('backupDr.download.titleLatestSuccess')).toBeInTheDocument();
    expect(screen.getByText(/backupDr\.health\.realPgDumpYes/)).toBeInTheDocument();
    expect(screen.queryByText('backupDr.backupStatus.simulatedSuccess')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.progress.finishedSimulatedOkDetail')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.download.scopeLatestSuccessFake')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.fakeMode.bannerRealBackupPrereq')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.download.reality.stub')).not.toBeInTheDocument();
  });

  it('Fake + PG_RESTORE_LIST_FAILED latest drill: stub explainer on dashboard — not prominent mystery failure', () => {
    vi.mocked(useGetApiAdminBackupStatusLatest).mockReturnValue(baseFakeStatusLatest() as never);
    vi.mocked(useGetApiAdminBackupRecoverabilitySummary).mockReturnValue(
      baseRecoverabilityFake({ lastSuccessfulRestoreProofAt: null }) as never,
    );
    vi.mocked(useGetApiAdminRestoreVerificationRunsLatest).mockReturnValue({
      data: {
        id: 'rv-fail',
        status: RestoreVerificationRunResponseDtoStatus.NUMBER_3,
        failureCode: 'PG_RESTORE_LIST_FAILED',
        failureDetail: 'could not read input',
        detailsJson: JSON.stringify({
          pgRestoreListFailureContext: { reason: FAKE_ADAPTER_STUB_NOT_PG_RESTORE_FORMAT },
        }),
        completedAt: '2026-01-10T12:00:00Z',
        dumpInspectionPassed: false,
        restoreAttemptExecuted: false,
      },
      isLoading: false,
      isError: false,
    } as never);

    render(wrap(<BackupDrDashboard />));

    expect(screen.getByText('backupDr.restoreVerification.fakePipeline.drillOutcomeTitle')).toBeInTheDocument();
    expect(screen.queryByText('backupDr.restoreVerification.drillFailedProminent')).not.toBeInTheDocument();
    expect(screen.getByText('backupDr.banner.restoreDrillStubListFailedExpected')).toBeInTheDocument();
  });

  it('operator-visible surfaces separate technical success, recoverability proof gap, and drill outcome', () => {
    vi.mocked(useGetApiAdminBackupStatusLatest).mockReturnValue(baseFakeStatusLatest() as never);
    vi.mocked(useGetApiAdminBackupRecoverabilitySummary).mockReturnValue(
      baseRecoverabilityFake({ lastSuccessfulRestoreProofAt: null }) as never,
    );
    vi.mocked(useGetApiAdminRestoreVerificationRunsLatest).mockReturnValue({
      data: {
        status: RestoreVerificationRunResponseDtoStatus.NUMBER_3,
        failureCode: 'PG_RESTORE_LIST_FAILED',
        failureDetail: 'x',
        detailsJson: JSON.stringify({
          pgRestoreListFailureContext: { reason: FAKE_ADAPTER_STUB_NOT_PG_RESTORE_FORMAT },
        }),
        completedAt: '2026-01-10T12:00:00Z',
        dumpInspectionPassed: false,
      },
      isLoading: false,
      isError: false,
    } as never);

    render(wrap(<BackupDrDashboard />));

    expect(screen.getByText('backupDr.backupStatus.simulatedSuccess')).toBeInTheDocument();
    expect(screen.getByText('backupDr.scan.proofTimestampsIncomplete')).toBeInTheDocument();
    expect(screen.getByText('backupDr.recoverability.proofBlock.backupStub')).toBeInTheDocument();
    expect(screen.getByText('backupDr.restoreVerification.fakePipeline.drillOutcomeTitle')).toBeInTheDocument();
  });

  it('Fake dashboard: configuration health lists setup diagnostic codes from API (machine-actionable prerequisites)', () => {
    vi.mocked(useGetApiAdminBackupStatusLatest).mockReturnValue(
      baseFakeStatusLatest({
        configurationHealth: {
          level: 'healthy',
          workerEnabled: true,
          effectiveAdapterKind: 'Fake',
          realPostgreSqlLogicalDumpConfigured: false,
          issues: [],
          diagnostics: [
            {
              code: 'BACKUP_SETUP_DEV_ADAPTER_FAKE_NO_REAL_PG_DUMP',
              severity: 'Information',
              message: 'Development: Backup:ExecutionAdapterKind=Fake — no pg_dump',
            },
          ],
        },
      }) as never,
    );
    vi.mocked(useGetApiAdminBackupRecoverabilitySummary).mockReturnValue(baseRecoverabilityFake() as never);
    vi.mocked(useGetApiAdminRestoreVerificationRunsLatest).mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
    } as never);

    render(wrap(<BackupDrDashboard />));

    expect(screen.getByText('backupDr.health.diagnosticsIntro')).toBeInTheDocument();
    expect(screen.getByText('BACKUP_SETUP_DEV_ADAPTER_FAKE_NO_REAL_PG_DUMP')).toBeInTheDocument();
  });

  it('PgDump degraded: setup diagnostics for missing prerequisites — no Fake-mode banner or Fake download scope', () => {
    vi.mocked(useGetApiAdminBackupStatusLatest).mockReturnValue({
      data: {
        latestRun: {
          id: 'run-pg-missing-cs',
          status: 3,
          adapterKind: 'PgDump',
          requestedAt: '2026-01-10T10:00:00Z',
          startedAt: '2026-01-10T10:00:01Z',
          completedAt: '2026-01-10T10:05:00Z',
        },
        configurationHealth: {
          level: 'degraded',
          workerEnabled: true,
          effectiveAdapterKind: 'PgDump',
          realPostgreSqlLogicalDumpConfigured: false,
          issues: ["Development: connection string 'DefaultConnection' is missing"],
          diagnostics: [
            {
              code: 'BACKUP_SETUP_PG_DUMP_CONNECTION_STRING_MISSING',
              severity: 'Warning',
              message: "Development: connection string 'DefaultConnection' is missing",
            },
          ],
        },
        artifactPipelinePolicy: {},
        restore: { isAutomatedRestoreAvailable: false, notes: '' },
        averageSucceededBackupDurationSeconds: null,
        averageSucceededBackupDurationSampleCount: null,
      },
      isLoading: false,
      isError: false,
      isFetching: false,
    } as never);

    vi.mocked(useGetApiAdminBackupRecoverabilitySummary).mockReturnValue(
      baseRecoverabilityFake({
        lastSuccessfulBackupRunId: 'run-pg-missing-cs',
        lastSuccessfulBackupRunIsSimulatedExecution: false,
        realPostgreSqlLogicalDumpConfigured: false,
      }) as never,
    );

    vi.mocked(useGetApiAdminBackupRunsId).mockReturnValue({
      data: {
        id: 'run-pg-missing-cs',
        status: 3,
        isSimulatedExecution: false,
        adapterKind: 'PgDump',
        artifacts: [
          {
            id: 'art-pg',
            artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
            isFilePresentForDownload: true,
            byteSize: 600_000,
          },
        ],
      },
      isLoading: false,
      isFetching: false,
      isError: false,
    } as never);

    vi.mocked(useGetApiAdminRestoreVerificationRunsLatest).mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
    } as never);

    render(wrap(<BackupDrDashboard />));

    expect(screen.queryByText('backupDr.fakeMode.bannerTitle')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.download.scopeLatestSuccessFake')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.download.titleLatestSuccessFake')).not.toBeInTheDocument();
    expect(screen.getByText('BACKUP_SETUP_PG_DUMP_CONNECTION_STRING_MISSING')).toBeInTheDocument();
    expect(screen.getByText('backupDr.health.diagnosticsIntro')).toBeInTheDocument();
  });
});
