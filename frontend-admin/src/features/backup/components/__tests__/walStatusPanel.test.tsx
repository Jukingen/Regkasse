/**
 * @vitest-environment jsdom
 */
import '@testing-library/jest-dom/vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, render, screen } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { WalStatusPanel } from '@/features/backup/components/WalStatusPanel';
import { WAL_STATUS_REFETCH_MS } from '@/features/backup/hooks/useWalStatus';
import type { WalArchiveHealthLevel, WalStatus } from '@/features/backup/logic/walStatusApi';

const { getWalStatusMock, notifyError } = vi.hoisted(() => ({
  getWalStatusMock: vi.fn(),
  notifyError: vi.fn(),
}));

vi.mock('@/features/backup/logic/walStatusApi', () => ({
  getWalStatus: () => getWalStatusMock(),
  getWalStatusQueryKey: () => ['/api/admin/backup/pitr/wal-status'] as const,
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({ error: notifyError, apiError: notifyError }),
}));

vi.mock('@/lib/hooks/useDateFormatter', () => ({
  useDateFormatter: () => ({
    formatDateTimeWithSeconds: (input: string) => `tz:${input}`,
  }),
}));

const copy: Record<string, string> = {
  'backupDr.walStatus.title': 'WAL-Archivierung',
  'backupDr.walStatus.lastSegment': 'Letztes Segment',
  'backupDr.walStatus.lag': 'Verzögerung',
  'backupDr.walStatus.segments24h': 'Segmente (24 h)',
  'backupDr.walStatus.empty': 'WAL-Archivierung ist noch nicht gestartet.',
  'backupDr.walStatus.loadFailed': 'Der WAL-Archivstatus konnte nicht geladen werden.',
  'backupDr.walStatus.lagValue': '{{seconds}} s',
  'backupDr.walStatus.segmentWhen': '{{when}} ({{age}})',
  'backupDr.walStatus.segmentsValue': '{{archived}} archiviert / {{failed}} fehlgeschlagen',
  'backupDr.walStatus.health.healthy': 'Gesund',
  'backupDr.walStatus.health.degraded': 'Beeinträchtigt',
  'backupDr.walStatus.health.critical': 'Kritisch',
};

function translate(key: string, values?: Record<string, string | number>) {
  let text = copy[key] ?? key;
  if (values) {
    for (const [name, value] of Object.entries(values)) {
      text = text.replaceAll(`{{${name}}}`, String(value));
    }
  }
  return text;
}

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: translate,
    formatLocale: 'de-AT',
  }),
}));

function sample(level: WalArchiveHealthLevel, overrides: Partial<WalStatus> = {}): WalStatus {
  return {
    lastSuccessfulSegmentAtUtc: '2026-10-25T03:00:00.000Z',
    lastSuccessfulSegmentName: '0000000100000000000000AB',
    lastAttemptedSegmentAtUtc: '2026-10-25T03:00:00.000Z',
    lagSeconds: 42,
    archivedCountLast24h: 12,
    failedCountLast24h: 1,
    lastFailureReason: null,
    healthLevel: level,
    healthReason: 'WAL lag is under 5 minutes.',
    ...overrides,
  };
}

function renderPanel() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity } },
  });
  return render(
    <QueryClientProvider client={client}>
      <App>
        <WalStatusPanel />
      </App>
    </QueryClientProvider>
  );
}

describe('WalStatusPanel', () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('renders the three cards with mocked data', async () => {
    getWalStatusMock.mockResolvedValue(sample('Degraded'));
    renderPanel();

    expect(await screen.findByTestId('wal-status-last-segment')).toHaveTextContent('Letztes Segment');
    expect(screen.getByTestId('wal-status-last-segment')).toHaveTextContent('0000000100000000000000AB');
    expect(screen.getByTestId('wal-status-last-segment')).toHaveTextContent(
      'tz:2026-10-25T03:00:00.000Z'
    );
    expect(screen.getByTestId('wal-status-lag')).toHaveTextContent('Verzögerung');
    expect(screen.getByTestId('wal-status-lag')).toHaveTextContent('42 s');
    expect(screen.getByTestId('wal-status-segments')).toHaveTextContent('Segmente (24 h)');
    expect(screen.getByTestId('wal-status-segments')).toHaveTextContent(
      '12 archiviert / 1 fehlgeschlagen'
    );
  });

  it('reflects healthLevel on the badge', async () => {
    const tones: Record<WalArchiveHealthLevel, string> = {
      Healthy: 'green',
      Degraded: 'amber',
      Critical: 'red',
    };

    for (const level of ['Healthy', 'Degraded', 'Critical'] as const) {
      getWalStatusMock.mockResolvedValue(
        sample(level, { healthReason: `reason-${level}` })
      );
      const view = renderPanel();
      const badge = await screen.findByTestId('wal-status-health');
      expect(badge).toHaveAttribute('data-health', level);
      expect(badge).toHaveAttribute('data-tone', tones[level]);
      expect(badge).toHaveAttribute('data-reason', `reason-${level}`);
      view.unmount();
    }
  });

  it('shows the empty state when no segment has been archived', async () => {
    getWalStatusMock.mockResolvedValue(
      sample('Critical', {
        lastSuccessfulSegmentAtUtc: null,
        lastSuccessfulSegmentName: null,
        lagSeconds: null,
        archivedCountLast24h: 0,
        failedCountLast24h: 0,
        healthReason: 'wal-archive-disabled',
      })
    );
    renderPanel();

    expect(await screen.findByTestId('wal-status-empty')).toHaveTextContent(
      'WAL-Archivierung ist noch nicht gestartet.'
    );
    expect(screen.queryByTestId('wal-status-last-segment')).not.toBeInTheDocument();
    expect(screen.getByTestId('wal-status-health')).toHaveAttribute('data-health', 'Critical');
  });

  it('refetches after the React Query interval', async () => {
    vi.useFakeTimers();
    getWalStatusMock.mockResolvedValue(sample('Healthy', { healthReason: 'WAL archive is current.' }));
    renderPanel();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(getWalStatusMock).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(WAL_STATUS_REFETCH_MS);
    });
    expect(getWalStatusMock.mock.calls.length).toBeGreaterThanOrEqual(2);
  });
});
