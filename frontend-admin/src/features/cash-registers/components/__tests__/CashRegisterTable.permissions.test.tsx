import React from 'react';
import { describe, it, expect, vi, beforeAll } from 'vitest';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nProvider } from '@/i18n';
import { CashRegisterTable } from '@/features/cash-registers/components/CashRegisterTable';
import type { CashRegister } from '@/api/generated/model';

const sampleRegister: CashRegister = {
    id: 'reg-1',
    createdAt: '2026-01-01T00:00:00Z',
    registerNumber: 'KASSE-001',
    location: 'Hauptkasse',
    status: 1,
    startingBalance: 0,
    currentBalance: 0,
    lastBalanceUpdate: '2026-01-01T00:00:00Z',
};

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

function renderTable(
    props: Partial<React.ComponentProps<typeof CashRegisterTable>> = {},
) {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return render(
        <QueryClientProvider client={queryClient}>
            <I18nProvider>
            <CashRegisterTable
                registers={[sampleRegister]}
                canDecommission={false}
                statusLabel={() => 'Geschlossen'}
                onEdit={vi.fn()}
                onDecommission={vi.fn()}
                {...props}
            />
            </I18nProvider>
        </QueryClientProvider>,
    );
}

describe('CashRegisterTable permissions', () => {
    it('hides action buttons for view-only users', () => {
        renderTable({ canManage: false, canDecommission: false });

        expect(screen.getByText('Hauptkasse')).toBeInTheDocument();
        expect(screen.queryByRole('button', { name: /Details/i })).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: /Stilllegen/i })).not.toBeInTheDocument();
    });

    it('shows details and decommission for manager-level permissions', () => {
        renderTable({ canManage: true, canDecommission: true });

        expect(screen.getByRole('button', { name: /Details/i })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /Stilllegen/i })).toBeInTheDocument();
    });
});
