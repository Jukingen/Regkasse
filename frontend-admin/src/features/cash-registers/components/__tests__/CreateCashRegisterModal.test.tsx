import React from 'react';
import { describe, it, expect, vi, beforeAll, beforeEach } from 'vitest';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nProvider } from '@/i18n';
import { CreateCashRegisterModal } from '@/features/cash-registers/components/CreateCashRegisterModal';

const mockUseAuth = vi.fn();
const mockUseTenantList = vi.fn();
const mockMutateAsync = vi.fn();

vi.mock('@/features/auth/hooks/useAuth', () => ({
    useAuth: () => mockUseAuth(),
}));

vi.mock('@/features/tenancy/hooks/useTenantList', () => ({
    useTenantList: (opts: { enabled?: boolean }) => mockUseTenantList(opts),
}));

vi.mock('@/features/cash-registers/hooks/useCreateCashRegister', () => ({
    useCreateCashRegister: () => ({
        mutateAsync: mockMutateAsync,
        isPending: false,
    }),
}));

function renderModal(props: React.ComponentProps<typeof CreateCashRegisterModal>) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return render(
        <QueryClientProvider client={client}>
            <I18nProvider>
                <CreateCashRegisterModal {...props} />
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
    mockMutateAsync.mockReset();
});

describe('CreateCashRegisterModal', () => {
    it('shows Mandant dropdown for SuperAdmin without fixed tenant', () => {
        mockUseAuth.mockReturnValue({ user: { role: 'SuperAdmin', permissions: ['cash_register.manage'] } });

        renderModal({ visible: true, onClose: vi.fn() });

        expect(screen.getByLabelText(/^Mandant$/i)).toBeInTheDocument();
        expect(mockUseTenantList).toHaveBeenCalledWith(expect.objectContaining({ enabled: true }));
    });

    it('hides Mandant dropdown for Manager', () => {
        mockUseAuth.mockReturnValue({ user: { role: 'Manager', permissions: ['cash_register.manage'] } });

        renderModal({
            visible: true,
            tenantId: '11111111-1111-1111-1111-111111111111',
            onClose: vi.fn(),
        });

        expect(screen.queryByText(/^Mandant$/i)).not.toBeInTheDocument();
        expect(mockUseTenantList).toHaveBeenCalledWith(expect.objectContaining({ enabled: false }));
    });
});
