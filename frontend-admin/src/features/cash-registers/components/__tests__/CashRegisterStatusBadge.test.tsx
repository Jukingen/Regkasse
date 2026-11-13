import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import type { CashRegister } from '@/api/generated/model';
import { CashRegisterStatusBadge } from '@/features/cash-registers/components/CashRegisterStatusBadge';
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
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

function renderBadge(register: CashRegister) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <CashRegisterStatusBadge register={register} showTooltip={false} />
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('CashRegisterStatusBadge', () => {
  it('shows localized closed sub-status label', () => {
    renderBadge({
      status: 1,
      startbelegCreatedAt: '2026-01-01T00:00:00Z',
      lastBalanceUpdate: '2026-01-02T18:00:00Z',
    });
    expect(screen.getByText(/Schichtwechsel|Geschlossen/)).toBeInTheDocument();
  });

  it('shows localized open label', () => {
    renderBadge({ status: 2 });
    expect(screen.getByText(/Geöffnet/)).toBeInTheDocument();
  });
});
