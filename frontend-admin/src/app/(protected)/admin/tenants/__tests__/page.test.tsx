/**
 * Super-admin tenants list — deletion lifecycle UI (archive/restore/hard delete, includeDeleted).
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { TENANT_PERMANENT_DELETE_CONFIRM_PHRASE } from '@/features/super-admin/components/TenantPermanentDeleteModal';
import { I18nProvider } from '@/i18n';

import SuperAdminTenantsPage from '../page';

const mockListAdminTenantsPaged = vi.fn();
const mockSoftDeleteAdminTenant = vi.fn();
const mockRestoreAdminTenant = vi.fn();
const mockDeletePermanent = vi.fn();
const mockGetDeleteDependencies = vi.fn();
const mockExportTenantsCsv = vi.fn();

vi.mock('@/features/super-admin/api/adminTenants', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/super-admin/api/adminTenants')>();
  return {
    ...actual,
    listAdminTenantsPaged: (query?: unknown) => mockListAdminTenantsPaged(query),
    softDeleteAdminTenant: (id: string) => mockSoftDeleteAdminTenant(id),
    restoreAdminTenant: (id: string) => mockRestoreAdminTenant(id),
    exportTenantsCsv: (...args: unknown[]) => mockExportTenantsCsv(...args),
    impersonateAdminTenant: vi.fn(),
    updateAdminTenant: vi.fn(),
    updateTenantStatus: vi.fn(),
  };
});

vi.mock('@/api/generated/admin/admin', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/generated/admin/admin')>();
  return {
    ...actual,
    deleteApiAdminTenantsTenantIdPermanent: (...args: unknown[]) => mockDeletePermanent(...args),
    getApiAdminTenantsTenantIdDeleteDependencies: (...args: unknown[]) =>
      mockGetDeleteDependencies(...args),
  };
});

vi.mock('@/features/super-admin/components/CreateTenantWizard', () => ({
  CreateTenantWizard: () => null,
}));

vi.mock('@/features/super-admin/components/TenantLicenseBadge', () => ({
  TenantLicenseBadge: () => null,
}));

vi.mock('@/features/super-admin/components/ImpersonationRedirectOverlay', () => ({
  ImpersonationRedirectOverlay: () => null,
}));

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

const mockUseAuth = vi.fn();

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    message: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() },
    modal: { confirm: vi.fn() },
    notification: {},
  }),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}));

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

const activeTenant: AdminTenantListItem = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Cafe Demo',
  slug: 'cafe-demo',
  status: 'active',
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  registerCount: 1,
  userCount: 2,
};

const deletedTenant: AdminTenantListItem = {
  id: '22222222-2222-2222-2222-222222222222',
  name: 'Closed Shop',
  slug: 'closed-shop',
  status: 'cancelled',
  isActive: false,
  createdAt: '2025-06-01T00:00:00Z',
};

function paged(items: AdminTenantListItem[]) {
  return {
    items,
    totalCount: items.length,
    page: 1,
    pageSize: 20,
    totalPages: 1,
  };
}

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <App>
          <SuperAdminTenantsPage />
        </App>
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('SuperAdminTenantsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseAuth.mockReturnValue({ user: { id: 'super-1', role: 'SuperAdmin', permissions: [] } });
    mockListAdminTenantsPaged.mockResolvedValue(paged([activeTenant]));
    mockSoftDeleteAdminTenant.mockResolvedValue(undefined);
    mockRestoreAdminTenant.mockResolvedValue(undefined);
    mockDeletePermanent.mockResolvedValue(undefined);
    mockExportTenantsCsv.mockResolvedValue(new Blob(['Name\n'], { type: 'text/csv' }));
    mockGetDeleteDependencies.mockResolvedValue({
      tenantId: deletedTenant.id,
      tenantSlug: deletedTenant.slug,
      canHardDelete: true,
      hasFiscalFootprint: false,
      dependencies: { cashRegisters: 0 },
    });
  });

  it('renders includeDeleted toggle for Super Admin', async () => {
    renderPage();
    await waitFor(() => expect(mockListAdminTenantsPaged).toHaveBeenCalled());
    expect(mockListAdminTenantsPaged.mock.calls[0][0]).toMatchObject({ includeDeleted: false });
    expect(screen.getByText('Gelöschte anzeigen')).toBeInTheDocument();
    expect(screen.getByRole('switch')).toBeInTheDocument();
  });

  it('shows export CSV and filter controls', async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText('Cafe Demo')).toBeInTheDocument());
    expect(screen.getByRole('button', { name: /CSV exportieren/i })).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/Name oder Subdomain/i)).toBeInTheDocument();
  });

  it('hides includeDeleted toggle for non-Super Admin', async () => {
    mockUseAuth.mockReturnValue({
      user: { id: 'mgr-1', role: 'Manager', permissions: ['system.critical'] },
    });
    renderPage();
    await waitFor(() => expect(mockListAdminTenantsPaged).toHaveBeenCalled());
    expect(screen.queryByText('Gelöschte anzeigen')).not.toBeInTheDocument();
    expect(screen.queryByRole('switch')).not.toBeInTheDocument();
  });

  it('archive button opens modal and calls soft-delete API', async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText('Cafe Demo')).toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: /Archivieren/i }));

    const archiveTitle = await screen.findByText('Mandant archivieren?');
    const dialog = archiveTitle.closest('.ant-modal') as HTMLElement;
    fireEvent.click(within(dialog).getByRole('button', { name: /Mandant archivieren/i }));

    await waitFor(() => expect(mockSoftDeleteAdminTenant).toHaveBeenCalledWith(activeTenant.id));
  });

  it('hard delete submit disabled until slug, phrase and retention ack confirmed', async () => {
    mockListAdminTenantsPaged.mockResolvedValue(paged([deletedTenant]));
    renderPage();
    await waitFor(() => expect(screen.getByText('Closed Shop')).toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: /Endgültig löschen/i }));

    await waitFor(() => expect(mockGetDeleteDependencies).toHaveBeenCalled());
    const deleteTitle = await screen.findByText('Mandant endgültig löschen');
    const dialog = deleteTitle.closest('.ant-modal') as HTMLElement;
    await waitFor(() => expect(within(dialog).getByText('Bestätigung')).toBeInTheDocument());

    const modalOk = within(dialog).getByRole('button', { name: /Endgültig löschen/i });
    expect(modalOk).toBeDisabled();

    const inputs = within(dialog).getAllByRole('textbox');
    fireEvent.change(inputs[0], { target: { value: 'closed-shop' } });
    fireEvent.change(inputs[1], { target: { value: TENANT_PERMANENT_DELETE_CONFIRM_PHRASE } });
    fireEvent.click(within(dialog).getByRole('checkbox'));

    await waitFor(() => expect(modalOk).not.toBeDisabled());
  });

  it('restore button appears for deleted tenants', async () => {
    mockListAdminTenantsPaged.mockResolvedValue(paged([deletedTenant]));
    renderPage();
    await waitFor(() => expect(screen.getByText('Closed Shop')).toBeInTheDocument());
    expect(screen.getByRole('button', { name: /Wiederherstellen/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Endgültig löschen/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Archivieren/i })).not.toBeInTheDocument();
  });

  it('hides deletion actions for Manager without system.critical', async () => {
    mockUseAuth.mockReturnValue({
      user: { id: 'mgr-1', role: 'Manager', permissions: [] },
    });
    renderPage();

    await waitFor(() => expect(screen.getByText('Zugriff verweigert')).toBeInTheDocument());
    expect(mockListAdminTenantsPaged).not.toHaveBeenCalled();
    expect(screen.queryByRole('button', { name: /Archivieren/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Wiederherstellen/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Endgültig löschen/i })).not.toBeInTheDocument();
  });
});
