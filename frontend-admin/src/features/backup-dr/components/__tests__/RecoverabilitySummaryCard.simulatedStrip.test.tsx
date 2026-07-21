/**
 * Simüle ortam şeridi: üst pano Fake bilgisi varken tekrar şerit gösterimini omit ile keser.
 */
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import type { BackupRecoverabilitySummaryResponseDto } from '@/api/generated/model';
import { RecoverabilitySummaryCard } from '@/features/backup-dr/components/RecoverabilitySummaryCard';

beforeAll(() => {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
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

const t = (k: string) => k;
const summary: BackupRecoverabilitySummaryResponseDto = {
  lastSuccessfulBackupAt: '2026-01-01T00:00:00Z',
  lastSuccessfulArtifactVerificationAt: '2026-01-01T00:00:00Z',
  lastSuccessfulRestoreProofAt: '2026-01-01T00:00:00Z',
  lastSuccessfulBackupRunId: 'r1',
  lastSuccessfulBackupRunIsSimulatedExecution: false,
  realPostgreSqlLogicalDumpConfigured: true,
  latestRunAt: '2026-01-01T00:00:00Z',
  latestRunStatus: 3,
  latestRestoreRunAt: null,
  latestRestoreRunStatus: undefined,
  backupProofAgeSeconds: 1,
  restoreProofAgeSeconds: 1,
  backupExecutionReality: 'x',
  backupReadinessLevel: 'healthy',
  backupReadinessNarrative: '',
} as BackupRecoverabilitySummaryResponseDto;

describe('RecoverabilitySummaryCard — simulated strip dedupe', () => {
  it('hides simulatedEnvironmentStrip when omitSimulatedEnvironmentStrip is true', () => {
    render(
      <RecoverabilitySummaryCard
        summary={summary}
        loading={false}
        formatDt={() => '—'}
        formatLocale="en-US"
        backupStatusLabel={() => 'ok'}
        restoreStatusLabel={() => 'ok'}
        simulatedOperationalMode
        omitSimulatedEnvironmentStrip
        t={t}
      />
    );
    expect(
      screen.queryByText('backupDr.recoverability.simulatedEnvironmentStrip')
    ).not.toBeInTheDocument();
  });

  it('shows simulatedEnvironmentStrip when simulatedOperationalMode without omit', () => {
    render(
      <RecoverabilitySummaryCard
        summary={summary}
        loading={false}
        formatDt={() => '—'}
        formatLocale="en-US"
        backupStatusLabel={() => 'ok'}
        restoreStatusLabel={() => 'ok'}
        simulatedOperationalMode
        t={t}
      />
    );
    expect(
      screen.getByText('backupDr.recoverability.simulatedEnvironmentStrip')
    ).toBeInTheDocument();
  });
});
