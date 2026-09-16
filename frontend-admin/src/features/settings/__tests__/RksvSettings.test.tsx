import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { RksvSettings } from '@/features/settings/RksvSettings';
import { I18nProvider } from '@/i18n';

const fetchMonatsbelegPolicy = vi.hoisted(() => vi.fn());

vi.mock('@/features/settings/api/monatsbelegPolicy', () => ({
  monatsbelegPolicyQueryKey: ['admin', 'rksv', 'monatsbeleg-policy'],
  fetchMonatsbelegPolicy: (...args: unknown[]) => fetchMonatsbelegPolicy(...args),
  putMonatsbelegPolicy: vi.fn(),
}));

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => ({
    hasPermission: () => true,
  }),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    success: vi.fn(),
    successKey: vi.fn(),
    apiError: vi.fn(),
  }),
}));

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

describe('RksvSettings', () => {
  it('renders blocking-mode options from the loaded policy', async () => {
    fetchMonatsbelegPolicy.mockResolvedValue({
      blockingMode: 'GracePeriod',
      autoMonatsbelegEnabled: true,
      monatsbelegRetryCount: 3,
      useDecemberMonatsbelegAsJahresbeleg: true,
    });

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <I18nProvider>
          <RksvSettings />
        </I18nProvider>
      </QueryClientProvider>
    );

    expect(await screen.findByRole('button', { name: /Speichern|Save|Kaydet/i })).toBeInTheDocument();
    expect(screen.getByText(/Monatsbeleg — POS-Verkaufssperre|Monatsbeleg — POS sales block/i)).toBeInTheDocument();
  });
});
