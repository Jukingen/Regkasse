import '@testing-library/jest-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { FinanzOnlineOutboxWidget } from '@/features/dashboard/widgets/FinanzOnlineOutboxWidget';
import { I18nProvider } from '@/i18n/I18nProvider';
import { customInstance } from '@/lib/axios';

const authState = vi.hoisted(() => ({ role: 'SuperAdmin' as string }));

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
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

vi.mock('@/lib/axios', () => ({
  customInstance: vi.fn(async () => ({
    pendingCount: 4,
    failedLast24Hours: 2,
    lastSuccessfulSubmissionUtc: '2026-11-13T08:15:00Z',
  })),
}));

vi.mock('@/hooks/useAuthorizedQuery', () => ({
  useAuthorizationGate: () => ({ isAuthorized: true }),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => ({ user: { role: authState.role } }),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({ success: vi.fn(), error: vi.fn() }),
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({ modal: { confirm: vi.fn() } }),
}));

function renderWidget() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <I18nProvider>
        <FinanzOnlineOutboxWidget title="FinanzOnline-Outbox" />
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('FinanzOnlineOutboxWidget', () => {
  it('shows pending, failed last 24h, last success, and the Super Admin retry button', async () => {
    authState.role = 'SuperAdmin';
    renderWidget();

    expect(await screen.findByText('4')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
    expect(screen.getByText('Ausstehende Übermittlungen')).toBeInTheDocument();
    expect(screen.getByText('Fehlgeschlagen (24 Std.)')).toBeInTheDocument();
    expect(screen.getByText(/Letzte erfolgreiche Übermittlung/)).toBeInTheDocument();
    expect(screen.getByTestId('fon-outbox-retry-all')).toHaveTextContent(
      'Alle Fehlgeschlagenen erneut senden'
    );
    expect(customInstance).toHaveBeenCalledWith(
      expect.objectContaining({
        url: '/api/admin/finanzonline-outbox/health',
        method: 'GET',
      })
    );
  });

  it('hides retry all from a mandant admin', async () => {
    authState.role = 'Manager';
    renderWidget();

    expect(await screen.findByText('Ausstehende Übermittlungen')).toBeInTheDocument();
    expect(screen.queryByTestId('fon-outbox-retry-all')).not.toBeInTheDocument();
  });
});
