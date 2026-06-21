/**
 * Super-admin tenants list — deletion lifecycle UI (archive/restore/hard delete, includeDeleted).
 */
import React from 'react';
import { describe, it, expect, vi, beforeEach, beforeAll } from 'vitest';
import '@testing-library/jest-dom';
import { render, screen, within, waitFor, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nProvider } from '@/i18n';
import SuperAdminTenantsPage from '../page';
import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { TENANT_PERMANENT_DELETE_CONFIRM_PHRASE } from '@/features/super-admin/components/TenantPermanentDeleteModal';

const mockListAdminTenants = vi.fn();
const mockSoftDeleteAdminTenant = vi.fn();
const mockRestoreAdminTenant = vi.fn();
const mockDeletePermanent = vi.fn();
const mockGetDeleteDependencies = vi.fn();

vi.mock('@/features/super-admin/api/adminTenants', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@/features/super-admin/api/adminTenants')>();
    return {
        ...actual,
        listAdminTenants: (includeDeleted?: boolean) => mockListAdminTenants(includeDeleted),
        softDeleteAdminTenant: (id: string) => mockSoftDeleteAdminTenant(id),
        restoreAdminTenant: (id: string) => mockRestoreAdminTenant(id),
        impersonateAdminTenant: vi.fn(),
        updateAdminTenant: vi.fn(),
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
};

const deletedTenant: AdminTenantListItem = {
    id: '22222222-2222-2222-2222-222222222222',
    name: 'Closed Shop',
    slug: 'closed-shop',
    status: 'deleted',
    isActive: false,
    createdAt: '2025-06-01T00:00:00Z',
};

function renderPage() {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    return render(
        <QueryClientProvider client={queryClient}>
            <I18nProvider>
                <SuperAdminTenantsPage />
            </I18nProvider>
        </QueryClientProvider>,
    );
}

describe('SuperAdminTenantsPage', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        mockUseAuth.mockReturnValue({ user: { id: 'super-1', role: 'SuperAdmin', permissions: [] } });
        mockListAdminTenants.mockResolvedValue([activeTenant]);
        mockSoftDeleteAdminTenant.mockResolvedValue(undefined);
        mockRestoreAdminTenant.mockResolvedValue(undefined);
        mockDeletePermanent.mockResolvedValue(undefined);
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
        await waitFor(() => expect(mockListAdminTenants).toHaveBeenCalledWith(false));
        expect(screen.getByText('Gelöschte anzeigen')).toBeInTheDocument();
        expect(screen.getByRole('switch')).toBeInTheDocument();
    });

    it('hides includeDeleted toggle for non-Super Admin', async () => {
        mockUseAuth.mockReturnValue({
            user: { id: 'mgr-1', role: 'Manager', permissions: ['system.critical'] },
        });
        renderPage();
        await waitFor(() => expect(mockListAdminTenants).toHaveBeenCalled());
        expect(screen.queryByText('Gelöschte anzeigen')).not.toBeInTheDocument();
        expect(screen.queryByRole('switch')).not.toBeInTheDocument();
    });

    it('archive button opens modal and calls soft-delete API', async () => {
        renderPage();
        await waitFor(() => expect(screen.getByText('Cafe Demo')).toBeInTheDocument());

        fireEvent.click(screen.getByRole('button', { name: /Archivieren/i }));

        expect(await screen.findByText('Mandant archivieren?')).toBeInTheDocument();

        const modal = document.querySelector('.ant-modal') as HTMLElement;
        fireEvent.click(within(modal).getByRole('button', { name: /Mandant archivieren/i }));

        await waitFor(() => expect(mockSoftDeleteAdminTenant).toHaveBeenCalledWith(activeTenant.id));
    });

    it('hard delete submit disabled until slug, phrase and retention ack confirmed', async () => {
        mockListAdminTenants.mockResolvedValue([deletedTenant]);
        renderPage();
        await waitFor(() => expect(screen.getByText('Closed Shop')).toBeInTheDocument());

        fireEvent.click(screen.getByRole('button', { name: /Endgültig löschen/i }));

        const modal = await waitFor(() => document.querySelector('.ant-modal') as HTMLElement);
        await waitFor(() => expect(mockGetDeleteDependencies).toHaveBeenCalled());
        await waitFor(() => expect(within(modal).getByText('Bestätigung')).toBeInTheDocument());

        const modalOk = within(modal).getAllByRole('button', { name: /Endgültig löschen/i }).at(-1)!;
        expect(modalOk).toBeDisabled();

        const inputs = within(modal).getAllByRole('textbox');
        fireEvent.change(inputs[0], { target: { value: 'closed-shop' } });
        fireEvent.change(inputs[1], { target: { value: TENANT_PERMANENT_DELETE_CONFIRM_PHRASE } });
        fireEvent.click(within(modal).getByRole('checkbox'));

        await waitFor(() => expect(modalOk).not.toBeDisabled());
    });

    it('restore button appears for deleted tenants', async () => {
        mockListAdminTenants.mockResolvedValue([deletedTenant]);
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
        expect(mockListAdminTenants).not.toHaveBeenCalled();
        expect(screen.queryByRole('button', { name: /Archivieren/i })).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: /Wiederherstellen/i })).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: /Endgültig löschen/i })).not.toBeInTheDocument();
    });
});
