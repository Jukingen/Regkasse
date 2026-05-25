import React from 'react';
import { describe, it, expect, vi, beforeAll, beforeEach } from 'vitest';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nProvider } from '@/i18n';
import { CashRegisterSelector } from '@/features/cash-registers/components/CashRegisterSelector';

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
    mockUseTenantList.mockReturnValue({
        tenants: [
            { id: 'tenant-a', name: 'Cafe', slug: 'cafe' },
            { id: 'tenant-b', name: 'Bar', slug: 'bar' },
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

    it('manager scopes list to current tenant', () => {
        mockUseCurrentTenant.mockReturnValue({
            tenantId: 'tenant-a',
            isSuperAdminUser: false,
            tenantName: 'Cafe',
            tenantSlug: 'cafe',
        });

        renderSelector();

        expect(mockUseAdminCashRegisterList).toHaveBeenCalledWith(
            expect.objectContaining({
                tenantId: 'tenant-a',
                allowAllTenants: false,
                enabled: true,
            }),
        );
    });

    it('manager without tenant shows warning placeholder', () => {
        mockUseCurrentTenant.mockReturnValue({
            tenantId: null,
            isSuperAdminUser: false,
            tenantName: null,
            tenantSlug: null,
        });

        renderSelector();

        expect(screen.getByText('Kein Mandant ausgewählt')).toBeInTheDocument();
        expect(mockUseAdminCashRegisterList).toHaveBeenCalledWith(
            expect.objectContaining({ enabled: false }),
        );
    });

    it('super admin shows mandant picker when enabled', () => {
        mockUseCurrentTenant.mockReturnValue({
            tenantId: 'tenant-a',
            isSuperAdminUser: true,
            tenantName: 'Cafe',
            tenantSlug: 'cafe',
        });

        renderSelector();

        expect(mockUseTenantList).toHaveBeenCalledWith(expect.objectContaining({ enabled: true }));
        expect(screen.getAllByRole('combobox')).toHaveLength(2);
        expect(screen.getByText('Cafe (cafe)')).toBeInTheDocument();
    });
});
