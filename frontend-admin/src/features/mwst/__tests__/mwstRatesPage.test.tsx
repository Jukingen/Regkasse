import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { MwstRatesPage } from '@/features/mwst/MwstRatesPage';
import { I18nProvider } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getRequiredPermissionForPath } from '@/shared/auth/routePermissions';

const tenantId = '11111111-1111-1111-1111-111111111111';
const post = vi.fn();

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({ success: vi.fn(), error: vi.fn(), apiError: vi.fn() }),
}));

vi.mock('@/features/super-admin/api/adminTenants', () => ({
  listAdminTenants: vi.fn(async () => [{ id: tenantId, name: 'Zuerich', slug: 'zuerich' }]),
}));

vi.mock('@/api/generated/admin/admin', () => ({
  useGetApiAdminMwstTenantsTenantIdRates: (id: string) => ({
    data: id
      ? {
          tenantId: id,
          country: 'CH',
          applies: true,
          source: 'seed',
          rates: [
            { code: 'STANDARD', label: 'Normalsatz', rate: 8.1, effectiveFrom: '2024-01-01' },
          ],
        }
      : undefined,
    isLoading: false,
    isError: false,
    queryKey: ['mwst', id],
  }),
  usePostApiAdminMwstCanaryRollback: () => ({
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
          <MwstRatesPage />
        </App>
      </QueryClientProvider>
    </I18nProvider>,
  );
}

describe('MWST rates page', () => {
  beforeEach(() => {
    post.mockReset();
  });

  it('requires system.critical', () => {
    expect(getRequiredPermissionForPath('/admin/mwst')).toEqual([PERMISSIONS.SYSTEM_CRITICAL]);
  });

  it('renders rates and rollback calls POST', async () => {
    renderPage();
    expect(await screen.findByText('STANDARD')).toBeInTheDocument();
    expect(screen.getByText('Seed')).toBeInTheDocument();
    expect(screen.getByText('ja')).toBeInTheDocument();
    expect(screen.getByText('8.1')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Canary zurücknehmen' }));
    expect(post).toHaveBeenCalled();
  });
});
