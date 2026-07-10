import React from 'react';
import { describe, it, expect, vi, beforeAll, beforeEach } from 'vitest';
import '@testing-library/jest-dom';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nProvider } from '@/i18n';
import { CashRegisterSelector } from '@/components/CashRegisterSelector';

const mockUseCashRegisterSelection = vi.fn();

vi.mock('@/hooks/useCashRegisterSelection', () => ({
    useCashRegisterSelection: (opts: unknown) => mockUseCashRegisterSelection(opts),
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
    mockUseCashRegisterSelection.mockReturnValue({
        registers: [],
        registerOptions: [],
        selectedRegisterId: undefined,
        setSelectedRegisterId: vi.fn(),
        isLoading: false,
        error: null,
        isSingleRegister: false,
        hasMultipleRegisters: false,
    });
});

describe('Operational CashRegisterSelector', () => {
    it('shows skeleton while loading', () => {
        mockUseCashRegisterSelection.mockReturnValue({
            registers: [],
            registerOptions: [],
            selectedRegisterId: undefined,
            setSelectedRegisterId: vi.fn(),
            isLoading: true,
            error: null,
            isSingleRegister: false,
            hasMultipleRegisters: false,
        });

        const { container } = renderSelector();
        expect(container.querySelector('.ant-skeleton')).toBeInTheDocument();
    });

    it('renders single register as auto-selected text', () => {
        mockUseCashRegisterSelection.mockReturnValue({
            registers: [
                {
                    id: 'reg-1',
                    tenantId: 'tenant-a',
                    registerNumber: 'KASSE-001',
                    location: 'Theke',
                    status: 1,
                },
            ],
            registerOptions: [{ value: 'reg-1', label: 'KASSE-001 — Theke', register: {} as never }],
            selectedRegisterId: 'reg-1',
            setSelectedRegisterId: vi.fn(),
            isLoading: false,
            error: null,
            isSingleRegister: true,
            hasMultipleRegisters: false,
        });

        renderSelector({ label: 'Kasse' });

        expect(screen.getByText('KASSE-001')).toBeInTheDocument();
        expect(screen.getByText('Automatisch ausgewählt')).toBeInTheDocument();
        expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    });

    it('renders select for multiple registers', async () => {
        mockUseCashRegisterSelection.mockReturnValue({
            registers: [
                { id: 'reg-1', tenantId: 'tenant-a', registerNumber: 'K1', location: 'A', status: 1 },
                { id: 'reg-2', tenantId: 'tenant-a', registerNumber: 'K2', location: 'B', status: 1 },
            ],
            registerOptions: [
                { value: 'reg-1', label: 'K1 — A', register: {} as never },
                { value: 'reg-2', label: 'K2 — B', register: {} as never },
            ],
            selectedRegisterId: 'reg-1',
            setSelectedRegisterId: vi.fn(),
            isLoading: false,
            error: null,
            isSingleRegister: false,
            hasMultipleRegisters: true,
        });

        renderSelector({ showFormItem: false, required: false });

        await waitFor(() => {
            expect(screen.getByRole('combobox')).toBeInTheDocument();
        });
    });

    it('passes autoSelect flag from required prop', () => {
        renderSelector({ required: true });

        expect(mockUseCashRegisterSelection).toHaveBeenCalledWith(
            expect.objectContaining({ autoSelect: true, persistSelection: true, controlled: false }),
        );
    });

    it('marks selection as controlled when onChange is provided', () => {
        const onChange = vi.fn();
        renderSelector({ required: true, onChange });

        expect(mockUseCashRegisterSelection).toHaveBeenCalledWith(
            expect.objectContaining({ controlled: true, autoSelect: true }),
        );
    });

    it('passes autoSelect false when optional filter', () => {
        renderSelector({ required: false });

        expect(mockUseCashRegisterSelection).toHaveBeenCalledWith(
            expect.objectContaining({ autoSelect: false, persistSelection: true }),
        );
    });
});
