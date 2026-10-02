import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { App } from 'antd';
import React from 'react';
import { describe, expect, it, vi } from 'vitest';

import { PeppolSubmissionsPage } from '@/features/peppol/PeppolSubmissionsPage';
import { I18nProvider } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getRequiredPermissionForPath } from '@/shared/auth/routePermissions';

const tenantA = '11111111-1111-1111-1111-111111111111';
const tenantB = '22222222-2222-2222-2222-222222222222';

const rows = [
  row('q', tenantA, 'Queued', 'peppol-reserved', 'msg-queued'),
  row('s', tenantA, 'Sent', null, 'msg-sent'),
  row('a', tenantB, 'Ack', null, 'msg-ack'),
  row('t', tenantA, 'Failed', 'peppol-ack-timeout', 'msg-timeout'),
  row('e', tenantB, 'Failed', 'storecove-http-503', 'msg-503'),
];

function row(
  id: string,
  tenantId: string,
  status: string,
  failureReason: string | null,
  providerMessageId: string,
) {
  return {
    id,
    tenantId,
    invoiceId: `inv-${id}`,
    status,
    correlationId: `corr-${id}`,
    attemptedAtUtc: '2026-09-30T08:00:00Z',
    ackedAtUtc: status === 'Ack' ? '2026-09-30T09:00:00Z' : null,
    failureReason,
    providerMessageId,
    providerStatus: status === 'Ack' ? 'DELIVERED' : null,
  };
}

const listHook = vi.hoisted(() => vi.fn());
const detailHook = vi.hoisted(() => vi.fn());

vi.mock('@/api/generated/admin/admin', () => ({
  useGetApiAdminPeppolSubmissions: (...args: unknown[]) => listHook(...args),
  useGetApiAdminPeppolSubmissionsId: (...args: unknown[]) => detailHook(...args),
}));

vi.mock('@/features/super-admin/api/adminTenants', () => ({
  listAdminTenants: vi.fn(async () => [
    { id: tenantA, name: 'Alpha GmbH' },
    { id: tenantB, name: 'Beta GmbH' },
  ]),
}));

function renderPage() {
  listHook.mockImplementation((params?: { status?: string; tenantId?: string }) => {
    const items = rows.filter((item) => {
      if (params?.status && item.status !== params.status) return false;
      if (params?.tenantId && item.tenantId !== params.tenantId) return false;
      return true;
    });
    return {
      data: { items, total: items.length, limit: 50, offset: 0 },
      isLoading: false,
      isError: false,
      queryKey: ['peppol', params],
    };
  });
  detailHook.mockImplementation((id: string) => {
    const match = rows.find((item) => item.id === id);
    return {
      data: match
        ? { ...match, invoiceHref: `/invoices?invoiceId=${match.invoiceId}` }
        : undefined,
      isLoading: false,
      isError: false,
      queryKey: ['peppol', id],
    };
  });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <I18nProvider>
      <QueryClientProvider client={client}>
        <App>
          <PeppolSubmissionsPage />
        </App>
      </QueryClientProvider>
    </I18nProvider>,
  );
}

describe('peppol submissions queue', () => {
  it('requires system.critical', () => {
    expect(getRequiredPermissionForPath('/admin/peppol/submissions')).toEqual([PERMISSIONS.SYSTEM_CRITICAL]);
  });

  it('renders five rows and the reserved failure hint', async () => {
    renderPage();
    expect(await screen.findByText('Warteschlange')).toBeInTheDocument();
    expect(screen.getByText('Gesendet')).toBeInTheDocument();
    expect(screen.getByText('ACK')).toBeInTheDocument();
    expect(screen.getAllByText('Fehlgeschlagen')).toHaveLength(2);
    expect(screen.getAllByRole('row')).toHaveLength(6);
    expect(screen.getByText(/Canary zu aktivieren/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'inv-q' })).toHaveAttribute('href', '/invoices?invoiceId=inv-q');
  });

  it('filters the table by status', async () => {
    renderPage();
    expect(await screen.findByText('msg-queued')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('combobox', { name: 'Status' }));
    await userEvent.click(await screen.findByTitle('Sent'));

    expect(screen.getByText('msg-sent')).toBeInTheDocument();
    expect(screen.queryByText('msg-queued')).not.toBeInTheDocument();
    expect(screen.queryByText('msg-ack')).not.toBeInTheDocument();
    expect(listHook).toHaveBeenCalledWith(expect.objectContaining({ status: 'Sent' }));
  });
});
