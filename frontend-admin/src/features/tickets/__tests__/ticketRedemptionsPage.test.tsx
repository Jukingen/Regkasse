import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { describe, expect, it, vi } from 'vitest';

import { TicketRedemptionsPage } from '@/features/tickets/TicketRedemptionsPage';
import { I18nProvider } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getRequiredPermissionForPath } from '@/shared/auth/routePermissions';

const tenantA = '11111111-1111-1111-1111-111111111111';

vi.mock('@/api/admin/ticket-redemptions', () => ({
  useAdminTicketRedemptions: () => ({
    data: {
      items: [
        {
          id: 't1',
          tenantId: tenantA,
          displayCode: 'ABCDEF123456',
          status: 'Valid',
          validUntilUtc: '2027-09-30T00:00:00Z',
          redeemedAtUtc: null,
          redeemedByUserId: null,
        },
      ],
    },
    isLoading: false,
    isError: false,
  }),
}));

vi.mock('@/features/super-admin/api/adminTenants', () => ({
  listAdminTenants: vi.fn(async () => [{ id: tenantA, name: 'Alpha GmbH' }]),
}));

describe('ticket redemptions list', () => {
  it('requires product.view', () => {
    expect(getRequiredPermissionForPath('/admin/tickets/redemptions')).toEqual(
      PERMISSIONS.PRODUCT_VIEW
    );
  });

  it('renders hashed codes and status', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <I18nProvider>
        <QueryClientProvider client={client}>
          <App>
            <TicketRedemptionsPage />
          </App>
        </QueryClientProvider>
      </I18nProvider>
    );

    expect(await screen.findByText('Ticket-Einlösungen')).toBeTruthy();
    expect(screen.getByText('ABCDEF123456')).toBeTruthy();
    expect(screen.getByText('Gültig')).toBeTruthy();
    expect(await screen.findByText('Alpha GmbH')).toBeTruthy();
  });
});
