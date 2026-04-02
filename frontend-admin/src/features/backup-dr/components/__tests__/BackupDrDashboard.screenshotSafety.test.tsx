/**
 * Pano düzeyinde “yeşil ekran = DR tamam” regresyonu: yapılandırma iyi olsa bile
 * kurtarılabilirlik kanıtı / tatbikat / son çalıştırma ayrımı ve üst satır footnote kilitlenir.
 *
 * Orval üretimi axios üzerinden yan etki verdiği için importOriginal kullanılmaz;
 * yalnızca dashboard’un ihtiyaç duyduğu export’lar sahte modülde tanımlanır.
 */

import React from 'react';
import '@testing-library/jest-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';

/** Gerçek axios modülü test ortamında baseURL yokken fırlatır; kart zinciri yüklenmesi için. */
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

const t = (k: string) => k;

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t,
    formatLocale: 'en-US',
  }),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => ({
    user: {
      permissions: ['settings.manage'],
    },
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
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
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

describe('BackupDrDashboard — screenshot-level DR false-confidence', () => {
  beforeEach(() => {
    vi.mocked(useGetApiAdminBackupStatusLatest).mockReturnValue({
      data: {
        latestRun: {
          id: 'run-sim-1',
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

    vi.mocked(useGetApiAdminBackupRuns).mockReturnValue({
      data: { items: [] },
      isLoading: false,
      isError: false,
      isFetching: false,
    } as never);

    vi.mocked(useGetApiAdminBackupVerificationLatest).mockReturnValue({
      data: {
        status: 1,
        backupRunId: 'run-sim-1',
        completedAt: '2026-01-10T10:06:00Z',
        verifierSource: 'test',
      },
      isLoading: false,
      isError: false,
    } as never);

    vi.mocked(useGetApiAdminBackupRecoverabilitySummary).mockReturnValue({
      data: {
        lastSuccessfulBackupAt: '2026-01-10T10:05:00Z',
        lastSuccessfulArtifactVerificationAt: '2026-01-10T10:06:00Z',
        lastSuccessfulRestoreProofAt: null,
        lastSuccessfulBackupRunId: 'run-sim-1',
        lastSuccessfulBackupRunIsSimulatedExecution: true,
        realPostgreSqlLogicalDumpConfigured: true,
        latestRunAt: '2026-01-10T10:00:00Z',
        latestRunStatus: 3,
        latestRestoreRunAt: null,
        latestRestoreRunStatus: null,
        backupProofAgeSeconds: 3600,
        restoreProofAgeSeconds: null,
        backupExecutionReality: 'Simulated',
        backupReadinessLevel: 'healthy',
        backupReadinessNarrative: '',
      },
      isLoading: false,
      isError: false,
    } as never);

    vi.mocked(useGetApiAdminBackupRunsId).mockReturnValue({
      data: {
        id: 'run-sim-1',
        status: 3,
        isSimulatedExecution: true,
        adapterKind: 'Fake',
        artifacts: [],
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

    vi.mocked(useGetApiAdminRestoreVerificationRunsLatest).mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
    } as never);

    vi.mocked(usePostApiAdminBackupTrigger).mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    } as never);

    vi.mocked(usePostApiAdminRestoreVerificationTrigger).mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    } as never);
  });

  it('does not present an all-green DR story when config is healthy but recovery is not proven', () => {
    const { container } = render(wrap(<BackupDrDashboard />));

    expect(screen.getByText('backupDr.summary.backupHealthFootnoteFake')).toBeInTheDocument();
    expect(screen.getByText('backupDr.summary.restoreReadinessFootnoteFake')).toBeInTheDocument();
    expect(screen.getByText('backupDr.summary.fakeAdapterConfigNote')).toBeInTheDocument();

    const backupCard = screen.getByText('backupDr.summary.backupHealth').closest('.ant-card');
    expect(backupCard).toBeTruthy();
    const backupValue = within(backupCard as HTMLElement).getByText('backupDr.health.healthy');
    expect(backupValue).toHaveStyle({ color: 'rgb(22, 119, 255)' });

    expect(screen.getAllByText('backupDr.health.degraded').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('backupDr.readiness.levelCappedForOperatorTruth')).toBeInTheDocument();

    expect(screen.getByText('backupDr.operatorTruth.recoverabilityProofGap')).toBeInTheDocument();
    expect(screen.getByText('backupDr.recoverability.proofSectionTitle')).toBeInTheDocument();
    expect(screen.getByText('backupDr.recoverability.requestsSectionTitle')).toBeInTheDocument();

    expect(screen.getByText('backupDr.latestRun.title')).toBeInTheDocument();

    expect(screen.getByText('backupDr.banner.latestRunSimulatedNotProduction')).toBeInTheDocument();

    const successAlerts = container.querySelectorAll('.ant-alert-success');
    expect(successAlerts.length).toBe(0);
  });
});
