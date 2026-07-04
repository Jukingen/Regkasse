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
const mockUseCashRegisterSelection = vi.fn();
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

vi.mock('@/hooks/useCashRegisterSelection', () => ({
    useCashRegisterSelection: (opts: unknown) => mockUseCashRegisterSelection(opts),
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
    mockUseCashRegisterSelection.mockReturnValue({
        registers: [
            {
                id: 'reg-1',
                tenantId: 'tenant-a',
                registerNumber: 'KASSE-001',
                location: 'Theke',
                status: 2,
            },
        ],
        registerOptions: [],
        selectedRegister: null,
        selectedRegisterId: 'reg-1',
        setSelectedRegisterId: vi.fn(),
        isLoading: false,
        isFetching: false,
        error: null,
        hasMultipleRegisters: false,
        isSingleRegister: true,
        refetch: vi.fn(),
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
        mockUseCashRegisterSelection.mockReturnValue({
            registers: [
                {
                    id: 'reg-default',
                    tenantId: 'tenant-a',
                    registerNumber: 'KASSE-001',
                    location: 'Theke',
                    status: 2,
                    isDefaultForTenant: true,
                },
            ],
            registerOptions: [],
            selectedRegister: null,
            selectedRegisterId: 'reg-default',
            setSelectedRegisterId: vi.fn(),
            isLoading: false,
            isFetching: false,
            error: null,
            hasMultipleRegisters: false,
            isSingleRegister: true,
            refetch: vi.fn(),
        });

        renderSwitch();

        expect(screen.getByText('KASSE-001')).toBeInTheDocument();
        expect(mockUseCashRegisterSelection).toHaveBeenCalledWith(
            expect.objectContaining({ autoSelect: true, persistSelection: true }),
        );
    });
});
