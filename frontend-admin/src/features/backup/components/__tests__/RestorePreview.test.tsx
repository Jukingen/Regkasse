/**
 * @vitest-environment jsdom
 */
import '@testing-library/jest-dom/vitest';
import { render, screen } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { RestorePreview } from '@/features/backup/components/RestorePreview';

const useRestorePreviewMock = vi.fn();
const useRestoreComplianceCheckMock = vi.fn();

vi.mock('@/features/backup/hooks/useRestorePreview', () => ({
  useRestorePreview: (...args: unknown[]) => useRestorePreviewMock(...args),
}));

vi.mock('@/features/backup/hooks/useRestoreComplianceCheck', () => ({
  useRestoreComplianceCheck: (...args: unknown[]) => useRestoreComplianceCheckMock(...args),
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: (key: string, opts?: Record<string, string | number>) =>
      opts ? `${key}:${JSON.stringify(opts)}` : key,
    formatLocale: 'de-AT',
  }),
}));

describe('RestorePreview', () => {
  beforeEach(() => {
    useRestorePreviewMock.mockReset();
    useRestoreComplianceCheckMock.mockReset();
    useRestoreComplianceCheckMock.mockReturnValue({
      data: {
        succeeded: true,
        checks: [
          { name: 'SameTenant', passed: true, detail: 'same_tenant_ok' },
          { name: 'BackupIntegrity', passed: true, detail: 'integrity_ok' },
          { name: 'RksvValidationGate', passed: true },
        ],
      },
      succeeded: true,
      isLoading: false,
      isError: false,
    });
  });

  it('renders summary, compliance check, and table rows', () => {
    useRestorePreviewMock.mockReturnValue({
      data: {
        backupRunId: 'run-1',
        tables: 2,
        records: 150,
        sizeBytes: 2_097_152,
        sizeFormatted: '2.00 MB',
        logicalDumpAnalyzed: true,
        analysisMessage: null,
        changes: [
          {
            key: 'public.products',
            table: 'public.products',
            count: 100,
            changeKind: 'aligned',
            diff: 0,
          },
        ],
      },
      isLoading: false,
      isError: false,
    });

    render(
      <App>
        <RestorePreview backup={{ id: 'run-1' }} />
      </App>
    );

    expect(screen.getByText('backupDr.manualRestore.restorePreview.cardTitle')).toBeInTheDocument();
    expect(
      screen.getByText('backupDr.manualRestore.restorePreview.compliance.alertTitle')
    ).toBeInTheDocument();
    expect(
      screen.getByText(/backupDr\.manualRestore\.restorePreview\.compliance\.checks\.sameTenant/)
    ).toBeInTheDocument();
    expect(
      screen.getByText('backupDr.manualRestore.restorePreview.compliance.status.compliant')
    ).toBeInTheDocument();
    expect(
      screen.getByText(/backupDr.manualRestore.restorePreview.values.tables/)
    ).toBeInTheDocument();
    expect(screen.getByText('public.products')).toBeInTheDocument();
  });

  it('shows non-compliant status without inventing success', () => {
    useRestorePreviewMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
    });
    useRestoreComplianceCheckMock.mockReturnValue({
      data: {
        succeeded: false,
        code: 'CROSS_TENANT_RESTORE_FORBIDDEN',
        error: 'Cross-tenant',
        checks: [{ name: 'SameTenant', passed: false, detail: 'cross_tenant' }],
      },
      succeeded: false,
      isLoading: false,
      isError: false,
    });

    const onComplianceChange = vi.fn();
    render(
      <App>
        <RestorePreview backup={{ id: 'run-1' }} onComplianceChange={onComplianceChange} />
      </App>
    );

    expect(
      screen.getByText('backupDr.manualRestore.restorePreview.compliance.status.notCompliant')
    ).toBeInTheDocument();
    expect(onComplianceChange).toHaveBeenCalledWith(false);
  });

  it('shows load error state', () => {
    useRestorePreviewMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: true,
    });

    render(
      <App>
        <RestorePreview backup={{ id: 'run-1' }} />
      </App>
    );

    expect(
      screen.getByText('backupDr.manualRestore.restorePreview.loadFailed')
    ).toBeInTheDocument();
  });
});
