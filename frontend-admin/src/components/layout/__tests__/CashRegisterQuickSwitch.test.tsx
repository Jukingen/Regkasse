import React from 'react';
import { describe, it, expect, vi, beforeAll, beforeEach } from 'vitest';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nProvider } from '@/i18n';
import { CashRegisterQuickSwitch } from '@/components/layout/CashRegisterQuickSwitch';

const mockPush = vi.fn();
const mockUseCurrentTenant = vi.fn();
const mockUseAdminCashRegisterList = vi.fn();
const mockUseCashRegisters = vi.fn();
const mockUsePermissions = vi.fn();

vi.mock('next/navigation', () => ({
    useRouter: () => ({ push: mockPush }),
}));

vi.mock('@/features/tenancy/hooks/useCurrentTenant', () => ({
    useCurrentTenant: () => mockUseCurrentTenant(),
}));

vi.mock('@/features/cash-registers/hooks/useAdminCashRegisterList', () => ({
    useAdminCashRegisterList: (opts: unknown) => mockUseAdminCashRegisterList(opts),
}));

vi.mock('@/features/cash-registers/hooks/useCashRegisters', () => ({
    useCashRegisters: (tenantId: unknown, opts: unknown) => mockUseCashRegisters(tenantId, opts),
}));

vi.mock('@/shared/auth/usePermissions', () => ({
    usePermissions: () => mockUsePermissions(),
}));

function renderSwitch() {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return render(
        <QueryClientProvider client={client}>
            <I18nProvider>
                <CashRegisterQuickSwitch />
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
    mockPush.mockReset();
    sessionStorage.clear();
    mockUsePermissions.mockReturnValue({ canViewCashRegisters: true });
    mockUseCurrentTenant.mockReturnValue({
        tenantId: 'tenant-a',
        isSuperAdminUser: false,
        requiresTenantSelection: false,
    });
    mockUseAdminCashRegisterList.mockReturnValue({
        registers: [],
        isLoading: false,
    });
    mockUseCashRegisters.mockReturnValue({
        registers: [
            {
                id: 'reg-1',
                tenantId: 'tenant-a',
                registerNumber: 'KASSE-001',
                location: 'Theke',
                status: 2,
            },
        ],
        defaultRegister: null,
        selectedRegisterId: 'reg-1',
        isLoading: false,
    });
});

describe('CashRegisterQuickSwitch', () => {
    it('renders when registers are available', () => {
        renderSwitch();
        expect(screen.getByTestId('admin-header-cash-register-quick-switch')).toBeInTheDocument();
        expect(screen.getByText('KASSE-001')).toBeInTheDocument();
    });

    it('hides when user lacks cash register view permission', () => {
        mockUsePermissions.mockReturnValue({ canViewCashRegisters: false });
        renderSwitch();
        expect(screen.queryByTestId('admin-header-cash-register-quick-switch')).not.toBeInTheDocument();
    });

    it('hides for super admin until tenant is selected', () => {
        mockUseCurrentTenant.mockReturnValue({
            tenantId: null,
            isSuperAdminUser: true,
            requiresTenantSelection: true,
        });
        renderSwitch();
        expect(screen.queryByTestId('admin-header-cash-register-quick-switch')).not.toBeInTheDocument();
    });

    it('auto-selects the tenant default register in session storage', async () => {
        mockUseCashRegisters.mockReturnValue({
            registers: [
                {
                    id: 'reg-default',
                    tenantId: 'tenant-a',
                    registerNumber: 'KASSE-001',
                    location: 'Theke',
                    status: 2,
                    isDefaultForTenant: true,
                },
                {
                    id: 'reg-other',
                    tenantId: 'tenant-a',
                    registerNumber: 'KASSE-002',
                    location: 'Bar',
                    status: 1,
                    isDefaultForTenant: false,
                },
            ],
            defaultRegister: {
                id: 'reg-default',
                tenantId: 'tenant-a',
                registerNumber: 'KASSE-001',
                location: 'Theke',
                status: 2,
                isDefaultForTenant: true,
            },
            selectedRegisterId: 'reg-default',
            isLoading: false,
        });

        renderSwitch();

        expect(screen.getByText('KASSE-001')).toBeInTheDocument();
        expect(mockUseCashRegisters).toHaveBeenCalledWith(
            'tenant-a',
            expect.objectContaining({ syncQuickSwitch: true }),
        );
    });
});
