import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen, waitFor } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { describe, expect, it, vi } from 'vitest';

import { I18nProvider } from '@/i18n';

import AdminDemoRequestsPage from '../page';

vi.mock('@/features/super-admin/api/demoRequests', () => ({
  listDemoRequests: vi.fn().mockResolvedValue({
    items: [
      {
        id: 'req-1',
        name: 'Ada Lovelace',
        email: 'ada@example.at',
        company: 'Café Beispiel',
        industry: 'gastronomy',
        status: 'New',
        createdAtUtc: '2026-10-26T10:00:00Z',
        message: 'Bitte um eine Vorführung.',
      },
    ],
    total: 1,
    page: 1,
    pageSize: 20,
  }),
}));

describe('admin demo requests page', () => {
  it('renders the table columns', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <I18nProvider>
          <App>
            <AdminDemoRequestsPage />
          </App>
        </I18nProvider>
      </QueryClientProvider>,
    );

    expect(await screen.findByText('Ada Lovelace')).toBeInTheDocument();
    for (const heading of ['Name', 'E-Mail', 'Firma', 'Branche', 'Status', 'Eingegangen']) {
      expect(screen.getByRole('columnheader', { name: heading })).toBeInTheDocument();
    }
    await waitFor(() => {
      expect(screen.getByText('Neu')).toBeInTheDocument();
    });
  });
});
