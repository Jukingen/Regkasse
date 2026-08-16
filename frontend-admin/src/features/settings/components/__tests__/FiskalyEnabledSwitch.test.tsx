import '@testing-library/jest-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { FiskalyEnabledSwitch } from '@/features/settings/components/FiskalyEnabledSwitch';
import { I18nProvider } from '@/i18n';

const getFiskalySettings = vi.fn();

vi.mock('@/features/dashboard/api/fiskalyStatus', () => ({
  getFiskalySettings: (...args: unknown[]) => getFiskalySettings(...args),
  updateFiskalySettings: vi.fn(),
  getFiskalyStatus: vi.fn(),
}));

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => ({ canManageCashRegisters: true }),
}));

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

describe('FiskalyEnabledSwitch', () => {
  it('defaults to enabled before settings load', () => {
    getFiskalySettings.mockReturnValue(new Promise(() => undefined));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={client}>
        <I18nProvider>
          <FiskalyEnabledSwitch />
        </I18nProvider>
      </QueryClientProvider>
    );

    expect(screen.getByRole('switch')).toHaveAttribute('aria-checked', 'true');
  });
});
