/**
 * @vitest-environment jsdom
 */
import '@testing-library/jest-dom/vitest';
import { render, screen } from '@testing-library/react';
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
    });

    render(
      <App>
        <BackupComplianceDashboard />
      </App>
    );

    expect(screen.getByText('backupDr.compliance.warningTitle')).toBeInTheDocument();
    expect(screen.getByText('backupDr.compliance.listTitle')).toBeInTheDocument();
  });
});
