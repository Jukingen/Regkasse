import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { App } from 'antd';
import React from 'react';
import { describe, expect, it, vi } from 'vitest';

import { TicketEventsPage } from '@/features/tickets/TicketEventsPage';
import { I18nProvider } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getRequiredPermissionForPath } from '@/shared/auth/routePermissions';

const createMock = vi.fn(async () => ({
  id: 'event-1',
  name: 'Kammerkonzert',
  venue: 'Saal 1',
  capacity: 20,
  isActive: true,
  startAtUtc: '2026-11-01T18:00:00.000Z',
  endAtUtc: '2026-11-01T20:00:00.000Z',
}));

vi.mock('@/api/admin/ticket-events', () => ({
  adminTicketEventQueryKeys: { all: ['admin', 'ticket-events'] },
  useAdminTicketEvents: () => ({ data: [], isLoading: false, isError: false }),
  createAdminTicketEvent: (body: unknown) => createMock(body),
}));

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => ({ hasPermission: () => true }),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({ success: vi.fn(), apiError: vi.fn() }),
}));

describe('ticket event editor', () => {
  it('requires product.view', () => {
    expect(getRequiredPermissionForPath('/admin/tickets/events')).toEqual(PERMISSIONS.PRODUCT_VIEW);
    expect(getRequiredPermissionForPath('/admin/tickets/events/event-1/seats')).toEqual(
      PERMISSIONS.PRODUCT_VIEW
    );
  });

  it('saves a new event', async () => {
    const user = userEvent.setup();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <I18nProvider>
        <QueryClientProvider client={client}>
          <App>
            <TicketEventsPage />
          </App>
        </QueryClientProvider>
      </I18nProvider>
    );

    await user.type(await screen.findByLabelText('Name'), 'Kammerkonzert');
    await user.type(screen.getByLabelText('Ort'), 'Saal 1');
    await user.click(screen.getByRole('button', { name: 'Speichern' }));

    await waitFor(() =>
      expect(createMock).toHaveBeenCalledWith(
        expect.objectContaining({
          name: 'Kammerkonzert',
          venue: 'Saal 1',
          capacity: 20,
          isActive: true,
        })
      )
    );
  });
});
