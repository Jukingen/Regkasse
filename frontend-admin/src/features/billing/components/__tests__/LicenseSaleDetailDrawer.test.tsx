import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React, { type ReactNode } from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import type { LicenseSaleResponse } from '@/api/generated/model';
import { LicenseSaleDetailDrawer } from '@/features/billing/components/LicenseSaleDetailDrawer';
import { LicenseSaleDetailPanel } from '@/features/billing/components/LicenseSaleDetailPanel';
import { I18nProvider } from '@/i18n';

const mockPush = vi.fn();
const mockGetAdminTenantById = vi.fn();
const mockUseBillingSale = vi.fn();

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush, back: vi.fn() }),
}));

vi.mock('@/features/billing/hooks/useBillingAccess', () => ({
  useBillingAccess: () => true,
}));

vi.mock('@/features/billing/hooks/useBillingSale', () => ({
  useBillingSale: (...args: unknown[]) => mockUseBillingSale(...args),
}));

vi.mock('@/features/super-admin/api/adminTenants', () => ({
  getAdminTenantById: (...args: unknown[]) => mockGetAdminTenantById(...args),
}));

const sale: LicenseSaleResponse = {
  id: 'sale-1',
  tenantId: 'tenant-1',
  tenantName: 'Cafe Central',
  tenantSlug: 'cafe-central',
  licenseKey: 'REGK-20261231-cafe-central-ABC',
  licensePlan: '12_months',
  licenseType: 'Starter',
  status: 'active',
  invoiceNumber: 'RE-2026-001',
  soldAtUtc: '2026-01-15T10:00:00.000Z',
  validFromUtc: '2026-01-15T10:00:00.000Z',
  validUntilUtc: '2027-01-15T10:00:00.000Z',
  priceNet: 100,
  vatRate: 20,
  vatAmount: 20,
  priceGross: 120,
  currency: 'EUR',
  soldBy: 'Super Admin',
  appliedToTenant: true,
  notes: 'Test note',
};

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
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

describe('LicenseSaleDetailPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders license, tenant, invoice, and usage sections with actions', () => {
    const onDownload = vi.fn();
    render(
      <Wrapper>
        <LicenseSaleDetailPanel
          sale={sale}
          tenant={{
            id: 'tenant-1',
            name: 'Cafe Central',
            slug: 'cafe-central',
            status: 'active',
            isActive: true,
            createdAt: '2026-01-01T00:00:00Z',
            cashRegisterCount: 3,
            activeUserCount: 8,
          }}
          onDownloadInvoice={onDownload}
          showFullPageLink
        />
      </Wrapper>
    );

    expect(screen.getByText('REGK-20261231-cafe-central-ABC')).toBeInTheDocument();
    expect(screen.getByText('RE-2026-001')).toBeInTheDocument();
    expect(screen.getByText('cafe-central')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('8')).toBeInTheDocument();
    expect(screen.getByText('Test note')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Lizenz verlängern|Extend license|Lisansı uzat/i }));
    expect(mockPush).toHaveBeenCalledWith('/admin/billing/sales/new?tenantId=tenant-1');

    fireEvent.click(screen.getByRole('button', { name: /Mandant anzeigen|View tenant|Kiracıyı göster/i }));
    expect(mockPush).toHaveBeenCalledWith('/admin/tenants/tenant-1');

    fireEvent.click(
      screen.getByRole('button', { name: /Rechnung herunterladen|Download invoice|Faturayı indir/i })
    );
    expect(onDownload).toHaveBeenCalled();
  });
});

describe('LicenseSaleDetailDrawer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseBillingSale.mockReturnValue({ data: sale, isLoading: false });
    mockGetAdminTenantById.mockResolvedValue({
      id: 'tenant-1',
      name: 'Cafe Central',
      slug: 'cafe-central',
      status: 'active',
      isActive: true,
      createdAt: '2026-01-01T00:00:00Z',
      cashRegisterCount: 2,
      activeUserCount: 5,
    });
  });

  it('opens drawer and loads tenant usage stats', async () => {
    render(
      <Wrapper>
        <LicenseSaleDetailDrawer
          open
          saleId="sale-1"
          initialSale={sale}
          onClose={vi.fn()}
        />
      </Wrapper>
    );

    expect(await screen.findByText('RE-2026-001')).toBeInTheDocument();
    await waitFor(() => {
      expect(mockGetAdminTenantById).toHaveBeenCalledWith('tenant-1');
    });
    expect(await screen.findByText('2')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
  });
});
