/**
 * @vitest-environment jsdom
 */
import '@testing-library/jest-dom/vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { RecoverabilityVerifySection } from '@/features/backup-dr/components/RecoverabilityVerifySection';

const useSummary = vi.hoisted(() => vi.fn());
const useRuns = vi.hoisted(() => vi.fn());

vi.mock('@/i18n', () => ({
  useI18n: () => ({ t: (key: string) => key, formatLocale: 'de-AT' }),
}));

vi.mock('@/features/tenancy/providers/TenantProvider', () => ({
  useTenantProviderValue: () => ({ tenantId: 'tenant-1' }),
}));

vi.mock('@/features/backup/hooks/useBackupKnownGaps', () => ({
  useBackupKnownGaps: () => ({
    data: { acceptance: { acceptedGaps: ['scheduled-drill-not-run'] }, gaps: [] },
  }),
}));

vi.mock('@/api/generated/admin-backup/admin-backup', () => ({
  useGetApiAdminBackupRecoverabilitySummary: (options: unknown) => useSummary(options),
}));

vi.mock('@/features/backup/hooks/useRestoreVerificationRuns', () => ({
  useRestoreVerificationRuns: (...args: unknown[]) => useRuns(...args),
}));

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

describe('backup dashboard recoverability check', () => {
  it('renders the summary and an amber drift badge when the last passed drill differs', () => {
    useSummary.mockReturnValue({
      data: {
        lastSuccessfulRestoreProofAt: '2026-10-01T00:00:00.000Z',
        restoreProofAgeSeconds: 172800,
      },
      isSuccess: true,
    });
    useRuns.mockReturnValue({
      data: {
        items: [
          {
            id: 'drill-new',
            status: 'Succeeded',
            triggerSource: 'Scheduled',
            requestedAt: '2026-10-12T00:00:00.000Z',
            completedAt: '2026-10-12T00:05:00.000Z',
            durationMs: 1000,
          },
        ],
      },
    });

    render(
      <App>
        <RecoverabilityVerifySection />
      </App>
    );

    expect(useSummary).toHaveBeenCalledWith(
      expect.objectContaining({
        query: expect.objectContaining({ refetchInterval: 60000 }),
      })
    );
    expect(screen.getByTestId('recoverability-verify')).toBeInTheDocument();
    expect(screen.getAllByText('2026-10-01T00:00:00.000Z').length).toBeGreaterThan(0);
    expect(screen.getByText('1')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'backupDr.recoverabilityCheck.compare' }));
    expect(screen.getByTestId('recoverability-drift')).toBeInTheDocument();
  });
});
