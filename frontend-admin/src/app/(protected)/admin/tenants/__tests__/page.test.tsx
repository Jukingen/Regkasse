/**
 * Super-admin tenants list — deletion lifecycle UI (soft/restore/hard delete, includeDeleted).
 */
import React from 'react';
import { describe, it, expect, vi, beforeEach, beforeAll } from 'vitest';
import '@testing-library/jest-dom';
import { render, screen, within, waitFor, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nProvider } from '@/i18n';
import SuperAdminTenantsPage from '../page';
import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';

const mockListAdminTenants = vi.fn();
const mockSoftDeleteAdminTenant = vi.fn();
const mockRestoreAdminTenant = vi.fn();
const mockHardDeleteAdminTenant = vi.fn();

vi.mock('@/features/super-admin/api/adminTenants', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@/features/super-admin/api/adminTenants')>();
    return {
        ...actual,
        listAdminTenants: (includeDeleted?: boolean) => mockListAdminTenants(includeDeleted),
        softDeleteAdminTenant: (id: string) => mockSoftDeleteAdminTenant(id),
        restoreAdminTenant: (id: string) => mockRestoreAdminTenant(id),
        hardDeleteAdminTenant: (id: string, confirmSlug: string) =>
            mockHardDeleteAdminTenant(id, confirmSlug),
        impersonateAdminTenant: vi.fn(),
        updateAdminTenant: vi.fn(),
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

vi.mock('@/features/auth/hooks/useAuth', () => ({
    useAuth: () => mockUseAuth(),
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
        mockHardDeleteAdminTenant.mockResolvedValue(undefined);
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

    it('soft delete button shows modal and calls API', async () => {
        renderPage();
        await waitFor(() => expect(screen.getByText('Cafe Demo')).toBeInTheDocument());

        fireEvent.click(screen.getByRole('button', { name: /Löschen/i }));

        const confirmTitle = await screen.findByText('Diesen Mandanten wirklich löschen?');
        expect(confirmTitle).toBeInTheDocument();

        const pop = confirmTitle.closest('.ant-popover') ?? confirmTitle.parentElement;
        const confirmButtons = within(pop as HTMLElement).getAllByRole('button', { name: /Löschen/i });
        fireEvent.click(confirmButtons[confirmButtons.length - 1]);

        await waitFor(() => expect(mockSoftDeleteAdminTenant).toHaveBeenCalledWith(activeTenant.id));
    });

    it('hard delete button disabled until tenant name confirmed', async () => {
        mockListAdminTenants.mockResolvedValue([deletedTenant]);
        renderPage();
        await waitFor(() => expect(screen.getByText('Closed Shop')).toBeInTheDocument());

        fireEvent.click(screen.getByRole('button', { name: /Endgültig löschen/i }));

        const modal = await waitFor(() => document.querySelector('.ant-modal'));
        expect(modal).toBeTruthy();

        const modalOk = within(modal as HTMLElement).getByRole('button', { name: /Endgültig löschen/i });
        expect(modalOk).toBeDisabled();

        const input = within(modal as HTMLElement).getByRole('textbox');
        fireEvent.change(input, { target: { value: 'wrong-slug' } });
        expect(modalOk).toBeDisabled();

        fireEvent.change(input, { target: { value: 'closed-shop' } });
        await waitFor(() => expect(modalOk).not.toBeDisabled());
    });

    it('restore button appears for deleted tenants', async () => {
        mockListAdminTenants.mockResolvedValue([deletedTenant]);
        renderPage();
        await waitFor(() => expect(screen.getByText('Closed Shop')).toBeInTheDocument());
        expect(screen.getByRole('button', { name: /Wiederherstellen/i })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /Endgültig löschen/i })).toBeInTheDocument();
        // Soft-delete uses label "Löschen" (accessible name deleteLöschen), not hard-delete.
        expect(screen.queryByRole('button', { name: /deleteLöschen/i })).not.toBeInTheDocument();
    });

    it('hides deletion actions for Manager without system.critical', async () => {
        mockUseAuth.mockReturnValue({
            user: { id: 'mgr-1', role: 'Manager', permissions: [] },
        });
        renderPage();

        await waitFor(() => expect(screen.getByText('Zugriff verweigert')).toBeInTheDocument());
        expect(mockListAdminTenants).not.toHaveBeenCalled();
        expect(screen.queryByRole('button', { name: /Löschen/i })).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: /Wiederherstellen/i })).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: /Endgültig löschen/i })).not.toBeInTheDocument();
    });
});
