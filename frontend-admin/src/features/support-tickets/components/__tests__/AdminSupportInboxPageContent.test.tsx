import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import React, { type ReactNode } from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { AdminSupportInboxPageContent } from '@/features/support-tickets/components/AdminSupportInboxPageContent';
import { I18nProvider } from '@/i18n';

vi.mock('next/navigation', () => ({
  usePathname: () => '/admin/support',
  useRouter: () => ({ push: vi.fn(), back: vi.fn() }),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    success: vi.fn(),
    error: vi.fn(),
  }),
}));

const fetchAllSupportTickets = vi.fn();
const fetchSupportInboxSummary = vi.fn();
const fetchAdminSupportTicket = vi.fn();

vi.mock('@/features/support-tickets/api/supportTickets', async () => {
  const actual = await vi.importActual<typeof import('@/features/support-tickets/api/supportTickets')>(
    '@/features/support-tickets/api/supportTickets'
  );
  return {
    ...actual,
    fetchAllSupportTickets: (...args: unknown[]) => fetchAllSupportTickets(...args),
    fetchSupportInboxSummary: (...args: unknown[]) => fetchSupportInboxSummary(...args),
    fetchAdminSupportTicket: (...args: unknown[]) => fetchAdminSupportTicket(...args),
    addAdminSupportTicketMessage: vi.fn(),
    updateAdminSupportTicketStatus: vi.fn(),
    assignSupportTicket: vi.fn(),
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

describe('AdminSupportInboxPageContent', () => {
  it('renders overview counts, ticket list, and detail actions', async () => {
    fetchSupportInboxSummary.mockResolvedValue({
      openCount: 3,
      inProgressCount: 1,
      resolvedCount: 4,
      closedCount: 2,
      byCategory: { Technical: 2, Billing: 1 },
      byPriority: { High: 1, Medium: 2 },
    });
    fetchAllSupportTickets.mockResolvedValue({
      items: [
        {
          id: 't-1',
          tenantId: 'tenant-1',
          tenantName: 'Cafe Demo',
          ticketNumber: 'SUP-260813-AB12CD',
          category: 'Technical',
          priority: 'High',
          status: 'Open',
          title: 'TSE offline',
          createdByUserId: 'user-1',
          createdAtUtc: '2026-08-13T10:00:00Z',
          updatedAtUtc: '2026-08-13T10:00:00Z',
          messageCount: 1,
        },
      ],
      totalCount: 1,
      openCount: 3,
    });
    fetchAdminSupportTicket.mockResolvedValue({
      id: 't-1',
      tenantId: 'tenant-1',
      tenantName: 'Cafe Demo',
      ticketNumber: 'SUP-260813-AB12CD',
      category: 'Technical',
      priority: 'High',
      status: 'Open',
      title: 'TSE offline',
      createdByUserId: 'user-1',
      createdAtUtc: '2026-08-13T10:00:00Z',
      updatedAtUtc: '2026-08-13T10:00:00Z',
      messageCount: 1,
      messages: [
        {
          id: 'm-1',
          authorUserId: 'user-1',
          authorDisplayName: 'Anna Huber',
          body: 'TSE antwortet nicht.',
          isStaffReply: false,
          isInternal: false,
          createdAtUtc: '2026-08-13T10:00:00Z',
        },
      ],
    });

    render(
      <Wrapper>
        <AdminSupportInboxPageContent />
      </Wrapper>
    );

    expect(screen.getByRole('heading', { name: 'Support-Tickets' })).toBeInTheDocument();
    expect(await screen.findByText('SUP-260813-AB12CD')).toBeInTheDocument();
    expect(screen.getByText('Cafe Demo')).toBeInTheDocument();
    expect(screen.getByText('TSE offline')).toBeInTheDocument();

    await userEvent.click(screen.getByText('TSE offline'));
    expect(await screen.findByRole('button', { name: 'Mir zuweisen' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Antwort senden' })).toBeInTheDocument();
    expect(screen.getByText('TSE antwortet nicht.')).toBeInTheDocument();
  });
});
