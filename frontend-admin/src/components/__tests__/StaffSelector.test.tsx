import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import { StaffSelector } from '@/components/StaffSelector';
import { I18nProvider } from '@/i18n';

const mockUseCashierFilterOptions = vi.fn();

vi.mock('@/features/reporting/hooks/useCashierFilterOptions', () => ({
  useCashierFilterOptions: () => mockUseCashierFilterOptions(),
}));

function renderStaffSelector(props: React.ComponentProps<typeof StaffSelector> = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <I18nProvider>
        <StaffSelector {...props} />
      </I18nProvider>
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
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

beforeEach(() => {
  mockUseCashierFilterOptions.mockReturnValue({
    options: [
      { value: 'user-1', label: 'Anna Admin' },
      { value: 'user-2', label: 'Bob Barista' },
    ],
    loading: false,
    onSearch: vi.fn(),
  });
});

describe('StaffSelector', () => {
  it('renders searchable select with cashier options hook', () => {
    renderStaffSelector();
    expect(screen.getByRole('combobox')).toBeInTheDocument();
    expect(mockUseCashierFilterOptions).toHaveBeenCalled();
  });

  it('respects disabled state', () => {
    renderStaffSelector({ disabled: true });
    expect(screen.getByRole('combobox')).toBeDisabled();
  });
});
