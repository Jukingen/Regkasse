import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import React, { type ReactNode } from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { TenantSupportPageContent } from '@/features/support-tickets/components/TenantSupportPageContent';
import { I18nProvider } from '@/i18n';

vi.mock('next/navigation', () => ({
  usePathname: () => '/tenant/support',
  useRouter: () => ({ push: vi.fn(), back: vi.fn() }),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    success: vi.fn(),
    error: vi.fn(),
  }),
}));

const fetchMySupportTickets = vi.fn();
const createSupportTicket = vi.fn();

vi.mock('@/features/support-tickets/api/supportTickets', async () => {
  const actual = await vi.importActual<typeof import('@/features/support-tickets/api/supportTickets')>(
    '@/features/support-tickets/api/supportTickets'
  );
  return {
    ...actual,
    fetchMySupportTickets: (...args: unknown[]) => fetchMySupportTickets(...args),
    fetchSupportTicket: vi.fn(),
    createSupportTicket: (...args: unknown[]) => createSupportTicket(...args),
    addSupportTicketMessage: vi.fn(),
    updateOwnSupportTicketStatus: vi.fn(),
  };
});

function Wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return (
    <QueryClientProvider client={queryClient}>
      <I18nProvider>{children}</I18nProvider>
    </QueryClientProvider>
  );
}

beforeAll(() => {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

describe('TenantSupportPageContent', () => {
  it('renders empty state and opens the create ticket modal', async () => {
    fetchMySupportTickets.mockResolvedValue({ items: [], totalCount: 0, openCount: 0 });

    render(
      <Wrapper>
        <TenantSupportPageContent />
      </Wrapper>
    );

    expect(screen.getByRole('heading', { name: 'Support' })).toBeInTheDocument();
    expect(await screen.findByText('Keine Support-Tickets')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Neues Ticket' }));
    expect(await screen.findByLabelText('Betreff')).toBeInTheDocument();
    expect(screen.getByLabelText('Nachricht')).toBeInTheDocument();
  });

  it('submits a new ticket from the create modal', async () => {
    fetchMySupportTickets.mockResolvedValue({ items: [], totalCount: 0, openCount: 0 });
    createSupportTicket.mockResolvedValue({
      id: 't-new',
      ticketNumber: 'SUP-260813-FFFFFF',
      category: 'Technical',
      priority: 'Medium',
      status: 'Open',
      title: 'Drucker defekt',
      messages: [],
    });

    render(
      <Wrapper>
        <TenantSupportPageContent />
      </Wrapper>
    );

    await userEvent.click(await screen.findByRole('button', { name: 'Neues Ticket' }));
    await userEvent.type(await screen.findByLabelText('Betreff'), 'Drucker defekt');
    await userEvent.type(
      screen.getByLabelText('Nachricht'),
      'Der Bondrucker reagiert nicht mehr.'
    );
    const submitButtons = screen.getAllByRole('button', { name: 'Neues Ticket' });
    await userEvent.click(submitButtons[submitButtons.length - 1]);

    await waitFor(() => {
      expect(createSupportTicket).toHaveBeenCalledWith({
        category: 'Technical',
        priority: 'Medium',
        title: 'Drucker defekt',
        message: 'Der Bondrucker reagiert nicht mehr.',
      });
    });
  });

  it('renders ticket rows with status badges', async () => {
    fetchMySupportTickets.mockResolvedValue({
      items: [
        {
          id: 't-1',
          tenantId: 'tenant-1',
          ticketNumber: 'SUP-260813-AB12CD',
          category: 'Billing',
          priority: 'High',
          status: 'Open',
          title: 'Invoice PDF missing',
          createdByUserId: 'user-1',
          createdAtUtc: '2026-08-13T10:00:00Z',
          updatedAtUtc: '2026-08-13T10:00:00Z',
          messageCount: 1,
        },
      ],
      totalCount: 1,
      openCount: 1,
    });

    render(
      <Wrapper>
        <TenantSupportPageContent />
      </Wrapper>
    );

    expect(await screen.findByText('SUP-260813-AB12CD')).toBeInTheDocument();
    expect(screen.getByText('Invoice PDF missing')).toBeInTheDocument();
    expect(screen.getByText('Offen')).toBeInTheDocument();
    expect(screen.getByText('Hoch')).toBeInTheDocument();
  });
});
