import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { KassenSicherheitPage } from '@/features/kassenSicherheit/KassenSicherheitPage';
import { I18nProvider } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getRequiredPermissionForPath } from '@/shared/auth/routePermissions';

const tenantId = '11111111-1111-1111-1111-111111111111';
const post = vi.fn();

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({ success: vi.fn(), error: vi.fn(), apiError: vi.fn() }),
}));

vi.mock('@/features/super-admin/api/adminTenants', () => ({
  listAdminTenants: vi.fn(async () => [{ id: tenantId, name: 'Berlin Cafe', slug: 'berlin' }]),
}));

vi.mock('@/api/generated/admin/admin', () => ({
  useGetApiAdminKassensicherheitStatus: () => ({
    data: {
      tenantId,
      flagEnabled: false,
      hasTenantOverride: false,
      deTssId: 'tss-1',
      deClientId: 'client-1',
      provider: 'not-configured',
      environment: 'TEST',
    },
    isLoading: false,
    isError: false,
    queryKey: ['ks-status'],
  }),
  useGetApiAdminKassensicherheitRecentTransactions: () => ({
    data: [
      {
        transactionId: 'tx-1',
        receiptNumber: 'DE-dev-1-1',
        status: 'recorded',
        createdAt: '2026-09-30T08:00:00Z',
      },
    ],
    isLoading: false,
    isError: false,
    queryKey: ['ks-recent'],
  }),
  usePostApiAdminKassensicherheitExportDsfinvk: () => ({
    mutate: post,
    isPending: false,
  }),
}));

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <I18nProvider>
      <QueryClientProvider client={client}>
        <App>
          <KassenSicherheitPage />
        </App>
      </QueryClientProvider>
    </I18nProvider>,
  );
}

describe('KassenSicherheit page', () => {
  beforeEach(() => {
    post.mockReset();
  });

  it('requires system.critical', () => {
    expect(getRequiredPermissionForPath('/admin/kassensicherheit')).toEqual([
      PERMISSIONS.SYSTEM_CRITICAL,
    ]);
  });

  it('renders status and DSFinV-K export calls POST', async () => {
    renderPage();
    expect(await screen.findByText('not-configured')).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Status' })).toBeInTheDocument();
    expect(screen.getByText('tss-1')).toBeInTheDocument();
    expect(screen.queryByRole('switch')).not.toBeInTheDocument();

    fireEvent.input(screen.getByLabelText('Beginn'), { target: { value: '2026-09-01' } });
    fireEvent.input(screen.getByLabelText('Ende'), { target: { value: '2026-09-30' } });
    fireEvent.click(screen.getByRole('button', { name: 'DSFinV-K exportieren' }));

    expect(post).toHaveBeenCalledWith({
      data: expect.objectContaining({
        tenantId,
        start: '2026-09-01',
        end: '2026-09-30',
      }),
    });
  });
});
