/**
 * TenantDeletePreparationPanel — Flow C dependency table and scenario coverage.
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen, waitFor, within } from '@testing-library/react';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import { TenantDeletePreparationPanel } from '@/features/super-admin/components/TenantDeletePreparationPanel';
import { I18nProvider } from '@/i18n';

const mockGetDeleteDependencies = vi.fn();

vi.mock('@/api/generated/admin/admin', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/generated/admin/admin')>();
  return {
    ...actual,
    getApiAdminTenantsTenantIdDeleteDependencies: (...args: unknown[]) =>
      mockGetDeleteDependencies(...args),
  };
});

vi.mock('@/features/super-admin/components/TenantArchiveConfirmModal', () => ({
  TenantArchiveConfirmModal: () => null,
}));

vi.mock('@/features/super-admin/components/TenantPermanentDeleteModal', () => ({
  TenantPermanentDeleteModal: () => null,
}));

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

const tenantId = '11111111-1111-1111-1111-111111111111';

function renderPanel(
  overrides: Partial<React.ComponentProps<typeof TenantDeletePreparationPanel>> = {}
) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <TenantDeletePreparationPanel
          tenantId={tenantId}
          tenantName="Cafe Demo"
          tenantSlug="cafe-demo"
          tenantStatus="active"
          {...overrides}
        />
      </I18nProvider>
    </QueryClientProvider>
  );
}

function getRowByCategory(table: HTMLElement, category: RegExp): HTMLElement {
  const row = within(table).getByRole('row', { name: category });
  return row;
}

beforeAll(() => {
  class ResizeObserverMock {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
  vi.stubGlobal('ResizeObserver', ResizeObserverMock);

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

describe('TenantDeletePreparationPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('Flow C: renders full dependency table with related links and archive CTA', async () => {
    mockGetDeleteDependencies.mockResolvedValue({
      tenantId,
      tenantSlug: 'cafe-demo',
      canHardDelete: false,
      failureCode: 'cash_registers_present',
      hasFiscalFootprint: false,
      dependencies: { cashRegisters: 1, users: 2, products: 5 },
    });

    renderPanel();

    await waitFor(() => expect(mockGetDeleteDependencies).toHaveBeenCalledWith(tenantId));

    const table = await screen.findByRole('table');
    expect(getRowByCategory(table, /Kassen/i)).toHaveTextContent('Blockierend');
    expect(getRowByCategory(table, /Benutzer/i)).toHaveTextContent('Vorhanden');
    expect(getRowByCategory(table, /Produkte/i)).toHaveTextContent('Vorhanden');
    expect(within(table).getAllByText(/Öffnen/i).length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: /Mandant archivieren/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Endgültig löschen/i })).not.toBeInTheDocument();
  });

  it('Scenario 2: empty tenant shows Keine for all rows and hard-delete when archived', async () => {
    mockGetDeleteDependencies.mockResolvedValue({
      tenantId,
      tenantSlug: 'empty-dev',
      canHardDelete: true,
      hasFiscalFootprint: false,
      dependencies: {},
    });

    renderPanel({ tenantStatus: 'deleted', tenantSlug: 'empty-dev' });

    const table = await screen.findByRole('table');
    await waitFor(() => {
      expect(within(table).getAllByText('Keine').length).toBeGreaterThan(5);
    });
    expect(screen.getByRole('button', { name: /Endgültig löschen/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Mandant archivieren/i })).not.toBeInTheDocument();
  });

  it('Scenario 3: fiscal footprint shows compliance alert and payment compliance badge', async () => {
    mockGetDeleteDependencies.mockResolvedValue({
      tenantId,
      tenantSlug: 'fiscal-cafe',
      canHardDelete: false,
      failureCode: 'fiscal_footprint_present',
      hasFiscalFootprint: true,
      dependencies: { payments: 12, receipts: 12 },
    });

    renderPanel({ tenantSlug: 'fiscal-cafe' });

    await waitFor(() => expect(screen.getByText(/Löschen nicht möglich/i)).toBeInTheDocument());
    expect(screen.getByText(/Compliance-Anforderungen/i)).toBeInTheDocument();

    const table = await screen.findByRole('table');
    expect(getRowByCategory(table, /Zahlungen/i)).toHaveTextContent('Compliance');
    expect(screen.getByRole('button', { name: /Mandant archivieren/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Endgültig löschen/i })).not.toBeInTheDocument();
  });

  it('Scenario 4: production policy shows compliance-only blocked message', async () => {
    mockGetDeleteDependencies.mockResolvedValue({
      tenantId,
      tenantSlug: 'prod-tenant',
      canHardDelete: false,
      failureCode: 'production_policy',
      hasFiscalFootprint: false,
      dependencies: {},
    });

    renderPanel({ tenantStatus: 'deleted', tenantSlug: 'prod-tenant' });

    await waitFor(() => expect(screen.getByText(/Löschen nicht möglich/i)).toBeInTheDocument());
    expect(screen.getByRole('button', { name: /Endgültig löschen/i })).toBeInTheDocument();
  });

  it('Edge case: audit logs show compliance badge; memberships show as present', async () => {
    mockGetDeleteDependencies.mockResolvedValue({
      tenantId,
      tenantSlug: 'audit-heavy',
      canHardDelete: false,
      hasFiscalFootprint: false,
      dependencies: { auditLogs: 42, memberships: 3 },
    });

    renderPanel({ tenantSlug: 'audit-heavy' });

    const table = await screen.findByRole('table');
    await waitFor(() => {
      expect(getRowByCategory(table, /Audit-Logs/i)).toHaveTextContent('Compliance');
      expect(getRowByCategory(table, /Mitgliedschaften/i)).toHaveTextContent('Vorhanden');
    });
  });
});
