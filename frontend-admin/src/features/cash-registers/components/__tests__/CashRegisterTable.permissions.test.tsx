import React from 'react';
import { describe, it, expect, vi, beforeAll, beforeEach } from 'vitest';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nProvider } from '@/i18n';
import { CashRegisterTable } from '@/features/cash-registers/components/CashRegisterTable';
import type { CashRegister } from '@/api/generated/model';
import { AppPermissions } from '@/shared/auth/permissions';

vi.mock('@/features/license/hooks/useLicense', () => ({
    useLicense: () => ({ licenseStatus: null }),
}));

vi.mock('@/hooks/useCanAccessPath', () => ({
    useCanAccessPath: () => false,
}));

const mockUsePermissions = vi.fn();

vi.mock('@/hooks/usePermissions', () => ({
    usePermissions: () => mockUsePermissions(),
}));

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
    global.ResizeObserver = class ResizeObserver {
        observe() {}
        unobserve() {}
        disconnect() {}
    } as unknown as typeof ResizeObserver;

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
    mockUsePermissions.mockReturnValue({
        isSuperAdmin: false,
        hasPermission: () => false,
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
                onRegisterAction={vi.fn()}
                {...props}
            />
            </I18nProvider>
        </QueryClientProvider>,
    );
}

describe('CashRegisterTable permissions', () => {
    it('hides Aktionen column without cash_register.manage', () => {
        renderTable({ onRegisterAction: undefined });

        expect(screen.getByText('Hauptkasse')).toBeInTheDocument();
        expect(screen.queryByRole('button', { name: /Details/i })).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: /Stilllegen/i })).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: /Aktionen/i })).not.toBeInTheDocument();
    });

    it('shows details and shift actions for Manager with cash_register.manage', () => {
        mockUsePermissions.mockReturnValue({
            isSuperAdmin: false,
            hasPermission: (permission: string) =>
                permission === AppPermissions.CashRegisterManage,
        });

        renderTable();

        expect(screen.getByRole('button', { name: /Details/i })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /Aktionen/i })).toBeInTheDocument();
        expect(screen.queryByRole('button', { name: /Stilllegen/i })).not.toBeInTheDocument();
    });
});
