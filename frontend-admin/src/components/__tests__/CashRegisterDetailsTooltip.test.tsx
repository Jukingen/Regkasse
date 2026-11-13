import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { CashRegisterDetailsTooltipContent } from '@/components/CashRegisterDetailsTooltip';
import { I18nProvider } from '@/i18n';

vi.mock('@/features/license/hooks/useLicense', () => ({
  useLicense: () => ({ licenseStatus: null }),
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
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

describe('CashRegisterDetailsTooltipContent', () => {
  it('renders full register details', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <I18nProvider>
          <CashRegisterDetailsTooltipContent
            register={{
              id: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
              tenantId: 'tenant-a',
              registerNumber: 'KASSE-001',
              location: 'Theke',
              status: 2,
              currentBalance: 125.5,
              tseHealthStatus: 'healthy',
            }}
          />
        </I18nProvider>
      </QueryClientProvider>
    );

    expect(screen.getByTestId('cash-register-details-tooltip')).toBeInTheDocument();
    expect(screen.getByText(/KASSE-001/)).toBeInTheDocument();
    expect(screen.getByText('aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee')).toBeInTheDocument();
    expect(screen.getByText('tenant-a')).toBeInTheDocument();
    expect(screen.getByText('Geöffnet')).toBeInTheDocument();
    expect(screen.getByText('TSE OK')).toBeInTheDocument();
    expect(screen.getByText(/125/)).toBeInTheDocument();
    expect(screen.getByTestId('cash-register-details-copy-all')).toHaveTextContent('Alles kopieren');
  });
});
