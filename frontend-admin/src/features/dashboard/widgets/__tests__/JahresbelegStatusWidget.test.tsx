import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { describe, expect, it, vi } from 'vitest';

import { JahresbelegStatusWidget } from '@/features/dashboard/widgets/JahresbelegStatusWidget';
import { I18nProvider } from '@/i18n';

vi.mock('@/hooks/useAuthorizedQuery', () => ({
  useAuthorizationGate: () => ({ isAuthorized: true }),
}));

vi.mock('@/features/dashboard/hooks/useJahresbelegTenantStatus', () => ({
  useJahresbelegTenantStatus: () => ({
    data: [
      {
        tenantId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        tenantName: 'Alpha',
        year: 2026,
        deadline: '2027-01-31',
        status: 'missing',
        warning: 'Jahresbeleg deadline is January 31. Please create it.',
      },
      {
        tenantId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        tenantName: 'Beta',
        year: 2026,
        deadline: '2027-01-31',
        status: 'created',
        warning: null,
      },
    ],
    isLoading: false,
    isFetching: false,
    isError: false,
    refetch: vi.fn(),
  }),
}));

describe('JahresbelegStatusWidget', () => {
  it('lists Jahresbeleg status per tenant', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <I18nProvider>
          <JahresbelegStatusWidget title="Jahresbeleg" />
        </I18nProvider>
      </QueryClientProvider>
    );

    expect(screen.getByText('Alpha')).toBeInTheDocument();
    expect(screen.getByText('Beta')).toBeInTheDocument();
    expect(screen.getByText('Offen')).toBeInTheDocument();
    expect(screen.getByText('Erstellt')).toBeInTheDocument();
    expect(screen.getByText('Die Jahresbeleg-Frist ist der 31. Januar. Bitte erstellen.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Jahresbeleg öffnen' })).toHaveAttribute(
      'href',
      '/rksv/jahresbeleg'
    );
  });
});
