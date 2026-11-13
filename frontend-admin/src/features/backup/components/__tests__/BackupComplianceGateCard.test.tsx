/**
 * @vitest-environment jsdom
 */
import '@testing-library/jest-dom/vitest';
import { render, screen } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { BackupComplianceGateCard } from '@/features/backup/components/BackupComplianceGateCard';

const drillAt = new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString();
const tenantBackupAt = '2026-10-01T00:00:00Z';

const { summaryState, drillState, knownState } = vi.hoisted(() => ({
  summaryState: {
    isSuccess: true,
    isError: false,
    isLoading: false,
    data: {
      lastSuccessfulRestoreProofAt: null as string | null,
      lastSuccessfulBackupAt: null as string | null,
    },
  },
  drillState: {
    isLoading: false,
    isError: false,
    data: {
      items: [] as Array<Record<string, unknown>>,
    },
  },
  knownState: {
    data: undefined as
      | {
          verdict: 'pass' | 'fail';
          allowsGateOverride: boolean;
          gaps: Array<{
            id: string;
            title: string;
            severity: string;
            open: boolean;
            accepted: boolean;
            acceptedAtUtc: string | null;
            acceptedBy: string | null;
            displayColor: string;
          }>;
        }
      | undefined,
  },
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: (key: string) => key,
    formatLocale: 'de-AT',
  }),
}));

vi.mock('@/api/generated/admin-backup/admin-backup', () => ({
  useGetApiAdminBackupRecoverabilitySummary: () => summaryState,
}));

vi.mock('@/features/backup/hooks/useBackupRuns', () => ({
  useBackupRuns: () => ({ data: { items: [] }, isLoading: false }),
}));

vi.mock('@/features/backup/hooks/useRestoreVerificationRuns', () => ({
  useRestoreVerificationRuns: () => drillState,
}));

vi.mock('@/features/backup/hooks/useBackupKnownGaps', () => ({
  useBackupKnownGaps: () => ({
    data: knownState.data,
    isLoading: false,
    isSuccess: knownState.data != null,
    isError: false,
  }),
}));

vi.mock('@/features/tenancy/providers/TenantProvider', () => ({
  useTenantProviderValue: () => ({ tenantId: '11111111-1111-1111-1111-111111111111' }),
}));

describe('BackupComplianceGateCard', () => {
  beforeEach(() => {
    summaryState.isSuccess = true;
    summaryState.isError = false;
    summaryState.isLoading = false;
    summaryState.data = {
      lastSuccessfulRestoreProofAt: null,
      lastSuccessfulBackupAt: tenantBackupAt,
    };
    drillState.data = { items: [] };
    knownState.data = undefined;
  });

  it('uses the dashboard scheduled proof and ignores a newer manual drill', () => {
    summaryState.data.lastSuccessfulRestoreProofAt = drillAt;
    drillState.data.items = [
      {
        id: 'manual-newer',
        status: 'Succeeded',
        triggerSource: 'Manual',
        completedAt: new Date().toISOString(),
        requestedAt: new Date().toISOString(),
      },
    ];

    render(
      <App>
        <BackupComplianceGateCard />
      </App>
    );

    const drill = screen.getByTestId('backup-gate-drill');
    expect(drill).toHaveAttribute('data-status', 'green');
    expect(drill).toHaveTextContent(drillAt);
    expect(drill).not.toHaveTextContent('backupDr.compliance.gate.missing');
  });

  it('stays red when recoverability has no scheduled proof, even if a manual drill succeeded', () => {
    drillState.data.items = [
      {
        id: 'manual-only',
        status: 2,
        triggerSource: 0,
        completedAt: drillAt,
        requestedAt: drillAt,
      },
    ];

    render(
      <App>
        <BackupComplianceGateCard />
      </App>
    );

    const drill = screen.getByTestId('backup-gate-drill');
    expect(drill).toHaveAttribute('data-status', 'red');
    expect(drill).toHaveTextContent('backupDr.compliance.gate.missing');

    const system = screen.getByTestId('backup-gate-systemBackup');
    expect(system).toHaveAttribute('data-status', 'red');
    expect(system).not.toHaveTextContent(tenantBackupAt);
  });

  it('renders an accepted open gap as amber and keeps the proof timestamp', () => {
    summaryState.data.lastSuccessfulRestoreProofAt = drillAt;
    knownState.data = {
      verdict: 'fail',
      allowsGateOverride: false,
      gaps: [
        {
          id: 'scheduled-drill-not-run',
          title: 'Scheduled restore drill is overdue',
          severity: 'error',
          open: true,
          accepted: true,
          acceptedAtUtc: '2026-10-01T00:00:00Z',
          acceptedBy: 'ops',
          displayColor: 'amber',
        },
        {
          id: 'external-archive-not-set',
          title: 'External archive root is not set',
          severity: 'warning',
          open: true,
          accepted: false,
          acceptedAtUtc: null,
          acceptedBy: null,
          displayColor: 'red',
        },
      ],
    };

    render(
      <App>
        <BackupComplianceGateCard />
      </App>
    );

    expect(screen.getByTestId('backup-known-gap-scheduled-drill-not-run')).toHaveAttribute(
      'data-status',
      'amber'
    );
    expect(screen.getByTestId('backup-known-gap-scheduled-drill-not-run')).toHaveTextContent(
      'backupDr.knownGapAcceptance.acceptedOn'
    );
    expect(screen.getByTestId('backup-known-gap-external-archive-not-set')).toHaveAttribute(
      'data-status',
      'red'
    );
    const drill = screen.getByTestId('backup-gate-drill');
    expect(drill).toHaveTextContent(drillAt);
    expect(screen.getByTestId('backup-known-gap-verdict')).toHaveAttribute('data-verdict', 'fail');
  });

  it('keeps an unaccepted open gap red', () => {
    knownState.data = {
      verdict: 'fail',
      allowsGateOverride: false,
      gaps: [
        {
          id: 'scheduled-drill-not-run',
          title: 'Scheduled restore drill is overdue',
          severity: 'error',
          open: true,
          accepted: false,
          acceptedAtUtc: null,
          acceptedBy: null,
          displayColor: 'red',
        },
      ],
    };

    render(
      <App>
        <BackupComplianceGateCard />
      </App>
    );

    expect(screen.getByTestId('backup-known-gap-scheduled-drill-not-run')).toHaveAttribute(
      'data-status',
      'red'
    );
    expect(screen.getByTestId('backup-gate-drill')).toHaveAttribute('data-status', 'red');
    expect(screen.getByTestId('backup-known-gap-verdict')).toHaveAttribute('data-verdict', 'fail');
  });
});
