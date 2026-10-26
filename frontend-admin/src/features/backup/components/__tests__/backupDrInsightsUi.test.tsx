/**
 * @vitest-environment jsdom
 */
import '@testing-library/jest-dom/vitest';
import { render, screen } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { describe, expect, it, vi } from 'vitest';

import { BackupComplianceGate } from '@/features/backup/components/BackupComplianceGate';
import { BackupCostCharts } from '@/features/backup/components/BackupCostCharts';
import { RestoreDrillVisualization } from '@/features/backup/components/RestoreDrillVisualization';

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: (key: string) => key,
    formatLocale: 'de-AT',
  }),
}));

function renderWithApp(node: React.ReactElement) {
  return render(<App>{node}</App>);
}

describe('backup DR UI', () => {
  it('renders the drill timeline', () => {
    renderWithApp(
      <RestoreDrillVisualization
        items={[
          {
            id: 'drill-1',
            at: '2026-10-01T08:00:00Z',
            durationMs: 90000,
            result: 'Succeeded',
            reason: 'checksum-ok',
            reportable: true,
          },
          {
            id: 'drill-2',
            at: '2026-09-20T08:00:00Z',
            durationMs: null,
            result: 'Failed',
            reason: '***',
            reportable: true,
          },
        ]}
        successRate={50}
        lastSuccessAt="2026-10-01T08:00:00Z"
        daysSince={11}
        nextDrillAt="2026-10-08T08:00:00Z"
        overdue={false}
        cadenceDays={7}
      />
    );

    expect(screen.getByTestId('backup-drill-drill-1')).toHaveTextContent('2026-10-01T08:00:00Z');
    expect(screen.getByTestId('backup-drill-drill-1')).toHaveTextContent('backupDr.drills.result.succeeded');
    expect(screen.getByTestId('backup-drill-drill-1')).toHaveTextContent('1m 30s');
    expect(screen.getByTestId('backup-drill-drill-1')).toHaveTextContent('checksum-ok');
    expect(screen.getByTestId('backup-drill-drill-2')).toHaveTextContent('backupDr.drills.result.failed');
    expect(screen.getByTestId('backup-drill-drill-2')).toHaveTextContent('backupDr.drills.reasonHidden');
    expect(screen.getByTestId('backup-drill-drill-2').textContent).not.toContain('C:\\backup');
  });

  it('shows an accepted overdue drill as amber without replacing the last success', () => {
    renderWithApp(
      <RestoreDrillVisualization
        items={[]}
        successRate={null}
        lastSuccessAt="2026-10-01T08:00:00Z"
        daysSince={20}
        nextDrillAt="2026-10-08T08:00:00Z"
        overdue
        overdueAccepted
        acceptedNote="backupDr.knownGapAcceptance.acceptedOn"
        cadenceDays={7}
      />
    );

    expect(screen.getByTestId('backup-drill-schedule')).toHaveAttribute('data-status', 'amber');
    expect(screen.getByTestId('backup-drill-accepted-note')).toHaveTextContent(
      'backupDr.knownGapAcceptance.acceptedOn'
    );
    expect(screen.getByText('2026-10-01T08:00:00Z')).toBeInTheDocument();
  });

  it('keeps an unaccepted overdue drill red', () => {
    renderWithApp(
      <RestoreDrillVisualization
        items={[]}
        successRate={null}
        lastSuccessAt="2026-10-01T08:00:00Z"
        daysSince={20}
        nextDrillAt="2026-10-08T08:00:00Z"
        overdue
        cadenceDays={7}
      />
    );

    expect(screen.getByTestId('backup-drill-schedule')).toHaveAttribute('data-status', 'red');
    expect(screen.queryByTestId('backup-drill-accepted-note')).not.toBeInTheDocument();
  });

  it('renders the cost charts from mock data', () => {
    renderWithApp(
      <BackupCostCharts
        trend={[{ date: '2026-10-01', costEur: 12.5 }]}
        tiers={[
          { name: 'Hot', sizeGb: 1.2, costEur: 1.2 },
          { name: 'Warm', sizeGb: 0.4, costEur: 0.4 },
          { name: 'Cold', sizeGb: 0.1, costEur: 0.1 },
        ]}
        retention={{ actual: 1.7, allHot: 2.4, savingsPercent: 29 }}
        monthlyEstimate={1.9}
      />
    );

    expect(screen.getByTestId('backup-cost-trend')).toHaveTextContent('2026-10-01: 12.50');
    expect(screen.getByTestId('backup-cost-tiers')).toBeInTheDocument();
    expect(screen.getByText('Hot: 1.200 GB')).toBeInTheDocument();
    expect(screen.getByText('Warm: 0.400 GB')).toBeInTheDocument();
    expect(screen.getByText('Cold: 0.100 GB')).toBeInTheDocument();
    const shares = ['Hot', 'Warm', 'Cold'].map((name) =>
      Number(screen.getByTestId(`backup-cost-share-${name}`).textContent?.replace('%', ''))
    );
    expect(shares.reduce((sum, share) => sum + share, 0)).toBe(100);
    expect(screen.getByTestId('backup-cost-retention')).toBeInTheDocument();
    expect(screen.getByText('€ 1.90')).toBeInTheDocument();
  });

  it('shows the compliance gate status color', () => {
    renderWithApp(
      <BackupComplianceGate
        gates={[
          { id: 'systemBackup', label: 'system', detail: '2026-10-01', color: 'red' },
          { id: 'legalHold', label: 'hold', detail: 'until', color: 'yellow' },
          { id: 'drill', label: 'drill', detail: '2026-10-11', color: 'green' },
        ]}
      />
    );

    expect(screen.getByTestId('backup-gate-systemBackup')).toHaveAttribute('data-status', 'red');
    expect(screen.getByTestId('backup-gate-legalHold')).toHaveAttribute('data-status', 'yellow');
    expect(screen.getByTestId('backup-gate-drill')).toHaveAttribute('data-status', 'green');
    expect(screen.getByText('backupDr.compliance.gate.red')).toBeInTheDocument();
  });
});
