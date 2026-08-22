/**
 * @vitest-environment jsdom
 */
import '@testing-library/jest-dom/vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { BackupComplianceDashboard } from '@/features/backup/components/BackupComplianceDashboard';

const useComplianceStatusMock = vi.fn();

vi.mock('@/features/backup/hooks/useComplianceStatus', () => ({
  useComplianceStatus: (...args: unknown[]) => useComplianceStatusMock(...args),
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: (key: string) => key,
    formatLocale: 'de-AT',
    textLocale: 'de',
  }),
}));

vi.mock('@/i18n/I18nProvider', () => ({
  useI18n: () => ({
    t: (key: string) => key,
    formatLocale: 'de-AT',
    textLocale: 'de',
  }),
}));

describe('BackupComplianceDashboard', () => {
  beforeEach(() => {
    useComplianceStatusMock.mockReset();
  });

  it('shows warning when not all compliant', () => {
    useComplianceStatusMock.mockReturnValue({
      data: {
        total: 2,
        compliant: 1,
        nonCompliant: 1,
        allCompliant: false,
        lastCheckUtc: '2026-07-17T12:00:00Z',
        backups: [
          {
            backupRunId: 'a',
            date: '2026-07-16T10:00:00Z',
            status: 'Succeeded',
            compliant: true,
            reason: 'system_dump_hash_present',
          },
          {
            backupRunId: 'b',
            date: '2026-07-15T10:00:00Z',
            status: 'Succeeded',
            compliant: false,
            reason: 'missing_sha256',
          },
        ],
      },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    });

    render(
      <App>
        <BackupComplianceDashboard />
      </App>
    );

    expect(screen.getByText('backupDr.compliance.warningTitle')).toBeInTheDocument();
    expect(screen.getByText('backupDr.compliance.listTitle')).toBeInTheDocument();
  });

  it('shows API error detail and retries on demand', () => {
    const refetch = vi.fn();
    useComplianceStatusMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: true,
      error: {
        message: 'Request failed with status code 403',
        response: {
          status: 403,
          data: { message: 'Permission denied', code: 'FORBIDDEN' },
        },
        normalized: { message: 'Permission denied' },
      },
      refetch,
      isFetching: false,
    });

    render(
      <App>
        <BackupComplianceDashboard />
      </App>
    );

    expect(screen.getByText('backupDr.compliance.loadFailed')).toBeInTheDocument();
    expect(screen.getByText(/HTTP 403/)).toBeInTheDocument();
    expect(screen.getByText(/FORBIDDEN/)).toBeInTheDocument();
    expect(screen.getByText(/Permission denied/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'common.buttons.retry' }));
    expect(refetch).toHaveBeenCalledTimes(1);
  });
});
