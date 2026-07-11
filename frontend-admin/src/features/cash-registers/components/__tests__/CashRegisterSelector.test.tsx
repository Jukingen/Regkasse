import React from 'react';
import { describe, it, expect, vi, beforeAll, beforeEach } from 'vitest';
import '@testing-library/jest-dom';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nProvider } from '@/i18n';
import { CashRegisterSelector } from '@/features/cash-registers/components/CashRegisterSelector';
import { FA_QUICK_CASH_REGISTER_STORAGE_KEY } from '@/features/cash-registers/constants/quickSwitch';

const mockUseCurrentTenant = vi.fn();
const mockUseTenantList = vi.fn();
const mockUseAdminCashRegisterList = vi.fn();

vi.mock('@/features/tenancy/hooks/useCurrentTenant', () => ({
    useCurrentTenant: () => mockUseCurrentTenant(),
}));

vi.mock('@/features/tenancy/hooks/useTenantList', () => ({
    useTenantList: (opts: { enabled?: boolean }) => mockUseTenantList(opts),
}));

vi.mock('@/features/cash-registers/hooks/useAdminCashRegisterList', () => ({
    useAdminCashRegisterList: (opts: unknown) => mockUseAdminCashRegisterList(opts),
}));

function renderSelector(props: React.ComponentProps<typeof CashRegisterSelector> = {}) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return render(
        <QueryClientProvider client={client}>
            <I18nProvider>
                <CashRegisterSelector {...props} />
            </I18nProvider>
        </QueryClientProvider>,
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
    sessionStorage.clear();
    mockUseTenantList.mockReturnValue({
        tenants: [
            { id: 'tenant-a', name: 'Cafe', slug: 'dev' },
            { id: 'tenant-b', name: 'Bar', slug: 'prod' },
        ],
        isLoading: false,
    });
    mockUseAdminCashRegisterList.mockReturnValue({
        registers: [],
        isLoading: false,
        error: null,
    });
});

describe('CashRegisterSelector', () => {
    it('super admin without mandant uses allowAllTenants list query', () => {
        mockUseCurrentTenant.mockReturnValue({
            tenantId: null,
            isSuperAdminUser: true,
            tenantName: null,
            tenantSlug: 'admin',
        });

        renderSelector({ showTenantPicker: false });

        expect(mockUseAdminCashRegisterList).toHaveBeenCalledWith(
            expect.objectContaining({
                allowAllTenants: true,
                enabled: true,
            }),
        );
    });

    it('manager loads registers via admin list with tenant-scoped default', () => {
        mockUseCurrentTenant.mockReturnValue({
            tenantId: 'tenant-a',
            isSuperAdminUser: false,
            tenantName: 'Cafe',
            tenantSlug: 'dev',
        });

        renderSelector();

        expect(mockUseAdminCashRegisterList).toHaveBeenCalledWith(
            expect.objectContaining({
                tenantId: 'tenant-a',
                allowTenantScopedDefault: true,
                enabled: true,
            }),
        );
    });

    it('manager without resolved tenant still loads registers via allowTenantScopedDefault', () => {
        mockUseCurrentTenant.mockReturnValue({
            tenantId: null,
            isSuperAdminUser: false,
            tenantName: null,
            tenantSlug: null,
        });

        renderSelector();

        expect(mockUseAdminCashRegisterList).toHaveBeenCalledWith(
            expect.objectContaining({
                allowTenantScopedDefault: true,
                enabled: true,
            }),
        );
        expect(screen.queryByText('Kein Mandant ausgewählt')).not.toBeInTheDocument();
    });

    it('super admin shows mandant picker when enabled', () => {
        mockUseCurrentTenant.mockReturnValue({
            tenantId: 'tenant-a',
            isSuperAdminUser: true,
            tenantName: 'Cafe',
            tenantSlug: 'dev',
        });

        renderSelector();

        expect(mockUseTenantList).toHaveBeenCalledWith(expect.objectContaining({ enabled: true }));
        expect(screen.getAllByRole('combobox')).toHaveLength(2);
        expect(screen.getByText('Cafe (cafe)')).toBeInTheDocument();
    });

    it('auto-selects default register and persists to session storage', async () => {
        const onChange = vi.fn();
        mockUseCurrentTenant.mockReturnValue({
            tenantId: 'tenant-a',
            isSuperAdminUser: false,
            tenantName: 'Cafe',
            tenantSlug: 'dev',
        });
        mockUseAdminCashRegisterList.mockReturnValue({
            registers: [
                {
                    id: 'reg-other',
                    registerNumber: 'K2',
                    location: 'Bar',
                    tenantId: 'tenant-a',
                    isDefaultForTenant: false,
                },
                {
                    id: 'reg-default',
                    registerNumber: 'K1',
                    location: 'Haupt',
                    tenantId: 'tenant-a',
                    isDefaultForTenant: true,
                },
            ],
            isLoading: false,
            error: null,
        });

        renderSelector({ onChange });

        await waitFor(() => {
            expect(onChange).toHaveBeenCalledWith('reg-default', 'K1', 'tenant-a');
        });
        expect(sessionStorage.getItem(FA_QUICK_CASH_REGISTER_STORAGE_KEY)).toBe('reg-default');
    });

    it('auto-selects the sole register when tenant has only one', async () => {
        const onChange = vi.fn();
        mockUseCurrentTenant.mockReturnValue({
            tenantId: 'tenant-a',
            isSuperAdminUser: false,
            tenantName: 'Cafe',
            tenantSlug: 'dev',
        });
        mockUseAdminCashRegisterList.mockReturnValue({
            registers: [
                {
                    id: 'reg-only',
                    registerNumber: 'K1',
                    location: 'Haupt',
                    tenantId: 'tenant-a',
                    isDefaultForTenant: false,
                },
            ],
            isLoading: false,
            error: null,
        });

        renderSelector({ onChange });

        await waitFor(() => {
            expect(onChange).toHaveBeenCalledWith('reg-only', 'K1', 'tenant-a');
        });
    });

    it('does not auto-select when multiple registers exist without a default', async () => {
        const onChange = vi.fn();
        mockUseCurrentTenant.mockReturnValue({
            tenantId: 'tenant-a',
            isSuperAdminUser: false,
            tenantName: 'Cafe',
            tenantSlug: 'dev',
        });
        mockUseAdminCashRegisterList.mockReturnValue({
            registers: [
                {
                    id: 'reg-1',
                    registerNumber: 'K1',
                    location: 'Haupt',
                    tenantId: 'tenant-a',
                    isDefaultForTenant: false,
                },
                {
                    id: 'reg-2',
                    registerNumber: 'K2',
                    location: 'Bar',
                    tenantId: 'tenant-a',
                    isDefaultForTenant: false,
                },
            ],
            isLoading: false,
            error: null,
        });

        renderSelector({ onChange });

        await waitFor(() => {
            expect(screen.getByRole('combobox')).toBeInTheDocument();
        });
        expect(onChange).not.toHaveBeenCalled();
        expect(sessionStorage.getItem(FA_QUICK_CASH_REGISTER_STORAGE_KEY)).toBeNull();
    });

    it('restores saved register from session storage when still valid', async () => {
        const onChange = vi.fn();
        sessionStorage.setItem(FA_QUICK_CASH_REGISTER_STORAGE_KEY, 'reg-saved');
        mockUseCurrentTenant.mockReturnValue({
            tenantId: 'tenant-a',
            isSuperAdminUser: false,
            tenantName: 'Cafe',
            tenantSlug: 'dev',
        });
        mockUseAdminCashRegisterList.mockReturnValue({
            registers: [
                {
                    id: 'reg-default',
                    registerNumber: 'K1',
                    location: 'Haupt',
                    tenantId: 'tenant-a',
                    isDefaultForTenant: true,
                },
                {
                    id: 'reg-saved',
                    registerNumber: 'K2',
                    location: 'Bar',
                    tenantId: 'tenant-a',
                    isDefaultForTenant: false,
                },
            ],
            isLoading: false,
            error: null,
        });

        renderSelector({ onChange });

        await waitFor(() => {
            expect(onChange).toHaveBeenCalledWith('reg-saved', 'K2', 'tenant-a');
        });
    });
});
