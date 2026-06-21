/**
 * TenantDetailDangerZone — archive and permanent delete modal wiring.
 */
import React from 'react';
import { describe, it, expect, vi, beforeEach, beforeAll } from 'vitest';
import '@testing-library/jest-dom';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nProvider } from '@/i18n';
import { TenantDetailDangerZone } from '@/features/super-admin/components/TenantDetailDangerZone';
import type { AdminTenantDetail } from '@/features/super-admin/api/adminTenants';
import { TENANT_PERMANENT_DELETE_CONFIRM_PHRASE } from '@/features/super-admin/components/TenantPermanentDeleteModal';

const mockSoftDeleteAdminTenant = vi.fn();
const mockDeletePermanent = vi.fn();
const mockGetDeleteDependencies = vi.fn();

vi.mock('@/features/super-admin/api/adminTenants', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@/features/super-admin/api/adminTenants')>();
    return {
        ...actual,
        softDeleteAdminTenant: (id: string) => mockSoftDeleteAdminTenant(id),
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

vi.mock('@/hooks/useAntdApp', () => ({
    useAntdApp: () => ({
        message: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() },
    }),
}));

vi.mock('next/link', () => ({
    default: ({ children, href }: { children: React.ReactNode; href: string }) => (
        <a href={href}>{children}</a>
    ),
}));

const activeTenant: AdminTenantDetail = {
    id: '11111111-1111-1111-1111-111111111111',
    name: 'Cafe Demo',
    slug: 'cafe-demo',
    status: 'active',
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
};

const deletedTenant: AdminTenantDetail = {
    ...activeTenant,
    status: 'deleted',
    isActive: false,
};

function renderZone(tenant: AdminTenantDetail, overrides: Partial<React.ComponentProps<typeof TenantDetailDangerZone>> = {}) {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    const onArchiveSuccess = vi.fn();
    const onPermanentDeleteSuccess = vi.fn();
    const onRestore = vi.fn();

    render(
        <QueryClientProvider client={queryClient}>
            <I18nProvider>
                <TenantDetailDangerZone
                    tenant={tenant}
                    onArchiveSuccess={onArchiveSuccess}
                    onPermanentDeleteSuccess={onPermanentDeleteSuccess}
                    onRestore={onRestore}
                    {...overrides}
                />
            </I18nProvider>
        </QueryClientProvider>,
    );

    return { onArchiveSuccess, onPermanentDeleteSuccess, onRestore };
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

describe('TenantDetailDangerZone', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        mockSoftDeleteAdminTenant.mockResolvedValue(undefined);
        mockDeletePermanent.mockResolvedValue(undefined);
        mockGetDeleteDependencies.mockResolvedValue({
            tenantId: deletedTenant.id,
            tenantSlug: deletedTenant.slug,
            canHardDelete: true,
            hasFiscalFootprint: false,
            dependencies: { cashRegisters: 0 },
        });
    });

    it('Flow A: archive opens modal, soft-deletes, and calls onArchiveSuccess', async () => {
        const { onArchiveSuccess } = renderZone(activeTenant);

        fireEvent.click(screen.getByRole('button', { name: /Mandant archivieren/i }));
        expect(await screen.findByText('Mandant archivieren?')).toBeInTheDocument();

        const modal = document.querySelector('.ant-modal') as HTMLElement;
        fireEvent.click(within(modal).getByRole('button', { name: /Mandant archivieren/i }));

        await waitFor(() => expect(mockSoftDeleteAdminTenant).toHaveBeenCalledWith(activeTenant.id));
        await waitFor(() => expect(onArchiveSuccess).toHaveBeenCalled());
    });

    it('Flow B: hard delete opens dependency modal and prefetches dependencies', async () => {
        renderZone(deletedTenant);

        fireEvent.click(screen.getByRole('button', { name: /Endgültig löschen/i }));

        const modal = await waitFor(() => document.querySelector('.ant-modal') as HTMLElement);
        await waitFor(() => expect(mockGetDeleteDependencies).toHaveBeenCalled());

        expect(within(modal).getByText('Mandant endgültig löschen')).toBeInTheDocument();
        expect(within(modal).getByText('Bestätigung')).toBeInTheDocument();

        const modalOk = within(modal).getAllByRole('button', { name: /Endgültig löschen/i }).at(-1)!;
        expect(modalOk).toBeDisabled();

        const inputs = within(modal).getAllByRole('textbox');
        fireEvent.change(inputs[0], { target: { value: 'cafe-demo' } });
        fireEvent.change(inputs[1], { target: { value: TENANT_PERMANENT_DELETE_CONFIRM_PHRASE } });
        fireEvent.click(within(modal).getByRole('checkbox'));

        await waitFor(() => expect(modalOk).not.toBeDisabled());
    });

    it('Flow B: blocked hard delete still opens modal with tooltip path', async () => {
        mockGetDeleteDependencies.mockResolvedValue({
            tenantId: deletedTenant.id,
            tenantSlug: deletedTenant.slug,
            canHardDelete: false,
            failureCode: 'cash_registers_present',
            hasFiscalFootprint: false,
            dependencies: { cashRegisters: 1 },
        });

        renderZone(deletedTenant);

        const hardDeleteButton = screen.getByRole('button', { name: /Endgültig löschen/i });
        expect(hardDeleteButton).not.toBeDisabled();

        fireEvent.click(hardDeleteButton);
        await waitFor(() => expect(mockGetDeleteDependencies).toHaveBeenCalled());
        expect(document.querySelector('.ant-modal')).toBeTruthy();
    });
});
