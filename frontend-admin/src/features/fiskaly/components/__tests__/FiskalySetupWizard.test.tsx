import '@testing-library/jest-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { FiskalySetupWizard } from '@/features/fiskaly/components/FiskalySetupWizard';
import { I18nProvider } from '@/i18n';

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
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
      removeEventListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

describe('FiskalySetupWizard', () => {
  it('starts on FON credentials when not authenticated', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <I18nProvider>
          <FiskalySetupWizard
            status={{
              enabled: true,
              isConfigured: true,
              environment: 'TEST',
              fon: { authenticated: false, authenticationStatus: 'NOT_AUTHENTICATED' },
              scu: { state: 'CREATED' },
              cashRegisters: [],
            }}
          />
        </I18nProvider>
      </QueryClientProvider>
    );

    expect(screen.getByText('FON-Teilnehmer-ID')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Weiter' })).toBeInTheDocument();
  });
});
