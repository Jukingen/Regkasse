import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { describe, expect, it, vi } from 'vitest';

import { KassenSicherheitCanaryPage } from '@/features/kassenSicherheit/KassenSicherheitCanaryPage';
import { I18nProvider } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getRequiredPermissionForPath } from '@/shared/auth/routePermissions';

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    success: vi.fn(),
    error: vi.fn(),
    apiError: vi.fn(),
  }),
}));

vi.mock('@/features/super-admin/api/adminTenants', () => ({
  listAdminTenants: vi.fn(async () => [{ id: '11111111-1111-1111-1111-111111111111', name: 'Canary', slug: 'canary' }]),
}));

vi.mock('@/features/kassenSicherheit/api', () => ({
  kassenSicherheitTenantsKey: ['admin', 'kassensicherheit', 'tenants'],
  kassenSicherheitStatusKey: (tenantId: string) => ['admin', 'kassensicherheit', 'status', tenantId],
  kassenSicherheitRecentKey: (tenantId: string) => ['admin', 'kassensicherheit', 'recent', tenantId],
  fetchKassenSicherheitStatus: vi.fn(async () => ({
    tenantId: '11111111-1111-1111-1111-111111111111',
    flagEnabled: false,
    hasTenantOverride: false,
    deTssId: 'tss-1',
    deClientId: 'client-1',
    provider: 'not-configured',
    environment: 'LIVE',
  })),
  fetchKassenSicherheitRecent: vi.fn(async () => [
    {
      transactionId: 'tx-1',
      receiptNumber: 'DE-dev-1-1',
      status: 'recorded',
      createdAt: '2026-09-26T00:00:00Z',
    },
  ]),
  putKassenSicherheitConfig: vi.fn(),
  postKassenSicherheitExport: vi.fn(async () => ({ status: 'PENDING' })),
}));

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <I18nProvider>
      <QueryClientProvider client={client}>
        <App>
          <KassenSicherheitCanaryPage />
        </App>
      </QueryClientProvider>
    </I18nProvider>,
  );
}

describe('kassenSicherheit canary page', () => {
  it('requires system.critical so Mandanten-Admin is denied', () => {
    expect(getRequiredPermissionForPath('/admin/kassensicherheit')).toEqual([PERMISSIONS.SYSTEM_CRITICAL]);
    expect(PERMISSIONS.SYSTEM_CRITICAL).toBe('system.critical');
  });

  it('renders selector, flag, TSS fields, recent list, and PENDING export', async () => {
    renderPage();
    expect(await screen.findByText('KassenSicherheit (DE)')).toBeInTheDocument();
    expect(screen.getByText('Fiscal.KassenSicherheitDe')).toBeInTheDocument();
    expect(screen.getByText('TSS-ID')).toBeInTheDocument();
    expect(screen.getByText('Client-ID')).toBeInTheDocument();
    expect(screen.getByText('DSFinV-K exportieren')).toBeInTheDocument();
    expect(screen.getByText('Letzte DE-Transaktionen')).toBeInTheDocument();
  });
});
