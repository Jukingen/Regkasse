import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React, { type ReactNode } from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { TenantInvoiceTable } from '@/features/tenant-invoices/components/TenantInvoiceTable';
import { I18nProvider } from '@/i18n';

const mockUseAuthorizedQuery = vi.fn();

vi.mock('next/navigation', () => ({
  usePathname: () => '/tenant/invoices',
  useRouter: () => ({ push: vi.fn(), back: vi.fn() }),
}));

vi.mock('@/hooks/useAuthorizedQuery', () => ({
  useAuthorizedQuery: (...args: unknown[]) => mockUseAuthorizedQuery(...args),
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    message: { open: vi.fn(), success: vi.fn(), error: vi.fn() },
  }),
}));

function Wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return (
    <QueryClientProvider client={queryClient}>
      <I18nProvider>{children}</I18nProvider>
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
      dispatchEvent: vi.fn(),
    })),
  });
});

describe('TenantInvoiceTable', () => {
  it('shows empty state when the tenant has no invoices', () => {
    mockUseAuthorizedQuery.mockReturnValue({
      data: {
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 20,
        totalPages: 0,
        activeCount: 0,
        cancelledCount: 0,
      },
      isLoading: false,
      isAuthorized: true,
    });

    render(
      <Wrapper>
        <TenantInvoiceTable />
      </Wrapper>
    );

    expect(screen.getByText('Keine Rechnungen gefunden')).toBeInTheDocument();
    expect(screen.getByLabelText('Status filtern')).toBeInTheDocument();
    expect(screen.getAllByLabelText('Zeitraum').length).toBeGreaterThan(0);
  });

  it('renders invoice rows with paid badge and download action', () => {
    mockUseAuthorizedQuery.mockReturnValue({
      data: {
        items: [
          {
            id: 'inv-1',
            invoiceNumber: '2026-001',
            issuedAt: '2026-01-15T10:00:00.000Z',
            invoiceDateUtc: '2026-01-15T10:00:00.000Z',
            amountNet: 100,
            vatAmount: 20,
            amountGross: 120,
            currency: 'EUR',
            status: 'paid',
            licenseKey: 'REGK-20261231-dev-abc',
            licensePlan: '12_months',
            downloadUrl: '/api/admin/billing/tenant-invoices/inv-1/pdf',
            pdfUrl: '/api/admin/billing/tenant-invoices/inv-1/pdf',
          },
        ],
        totalCount: 1,
        page: 1,
        pageSize: 20,
        totalPages: 1,
        activeCount: 1,
        cancelledCount: 0,
      },
      isLoading: false,
      isAuthorized: true,
    });

    render(
      <Wrapper>
        <TenantInvoiceTable />
      </Wrapper>
    );

    expect(screen.getByText('2026-001')).toBeInTheDocument();
    expect(screen.getByText('Bezahlt')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /PDF herunterladen/i })).toBeInTheDocument();
  });
});
