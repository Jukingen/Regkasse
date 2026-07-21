import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import dayjs from 'dayjs';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import { type ReportFilterValues, ReportFilters } from '@/components/ReportFilters';
import { I18nProvider } from '@/i18n';

const mockUseCashRegisterSelection = vi.fn();
const mockUseCashierFilterOptions = vi.fn();

vi.mock('@/hooks/useCashRegisterSelection', () => ({
  useCashRegisterSelection: (opts: unknown) => mockUseCashRegisterSelection(opts),
}));

vi.mock('@/features/reporting/hooks/useCashierFilterOptions', () => ({
  useCashierFilterOptions: () => mockUseCashierFilterOptions(),
}));

function renderFilters(props: Partial<React.ComponentProps<typeof ReportFilters>> = {}) {
  const onGenerate = vi.fn();
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const result = render(
    <QueryClientProvider client={client}>
      <I18nProvider>
        <ReportFilters onGenerate={onGenerate} {...props} />
      </I18nProvider>
    </QueryClientProvider>
  );
  return { ...result, onGenerate };
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
  mockUseCashRegisterSelection.mockReturnValue({
    registers: [
      {
        id: 'reg-1',
        tenantId: 'tenant-a',
        registerNumber: 'KASSE-001',
        location: 'Theke',
        status: 1,
      },
    ],
    registerOptions: [{ value: 'reg-1', label: 'KASSE-001' }],
    selectedRegisterId: 'reg-1',
    setSelectedRegisterId: vi.fn(),
    isLoading: false,
    error: null,
    isSingleRegister: true,
    hasMultipleRegisters: false,
  });
  mockUseCashierFilterOptions.mockReturnValue({
    options: [{ value: 'user-1', label: 'Max Mustermann' }],
    loading: false,
    onSearch: vi.fn(),
  });
});

describe('ReportFilters', () => {
  it('renders register, date range, and generate button', async () => {
    renderFilters();

    expect(screen.getByText('KASSE-001')).toBeInTheDocument();
    expect(screen.getByText(/Automatisch|auto/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Bericht|report|Rapor/i })).toBeInTheDocument();
  });

  it('calls onGenerate with register and date range on submit', async () => {
    const { onGenerate } = renderFilters({
      initialValues: {
        registerId: 'reg-1',
        dateRange: [dayjs('2026-01-01'), dayjs('2026-01-31')],
      },
    });

    fireEvent.click(screen.getByRole('button', { name: /Bericht|report|Rapor/i }));

    await waitFor(() => {
      expect(onGenerate).toHaveBeenCalled();
    });

    const values = onGenerate.mock.calls[0][0] as ReportFilterValues;
    expect(values.registerId).toBe('reg-1');
    expect(values.dateRange[0].format('YYYY-MM-DD')).toBe('2026-01-01');
  });

  it('shows staff filter when enabled', () => {
    renderFilters({ showStaffFilter: true });
    expect(screen.getByRole('combobox')).toBeInTheDocument();
  });

  it('shows export buttons when configured', () => {
    renderFilters({
      showExport: true,
      canExport: true,
      onExportPdf: vi.fn(),
      onExportExcel: vi.fn(),
    });

    expect(screen.getByRole('button', { name: /PDF/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Excel/i })).toBeInTheDocument();
  });

  it('supports live mode without generate button', () => {
    renderFilters({ mode: 'live' });
    expect(screen.queryByRole('button', { name: /Bericht|report|Rapor/i })).not.toBeInTheDocument();
  });

  it('uses optional register autoSelect only when register is required', () => {
    renderFilters({ registerRequired: false, registerAllowClear: true });

    expect(mockUseCashRegisterSelection).toHaveBeenCalledWith(
      expect.objectContaining({ autoSelect: false, persistSelection: true })
    );
  });
});
