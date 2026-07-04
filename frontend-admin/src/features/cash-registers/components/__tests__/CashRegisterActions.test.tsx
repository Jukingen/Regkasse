import React from 'react';
import { describe, it, expect, vi, beforeAll, beforeEach } from 'vitest';
import '@testing-library/jest-dom';
import { fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@/i18n';
import {
    CashRegisterActions,
    type CashRegisterActionKey,
} from '@/features/cash-registers/components/CashRegisterActions';
import type { CashRegister } from '@/api/generated/model';

const mockUsePermissions = vi.fn();

vi.mock('@/hooks/usePermissions', () => ({
    usePermissions: () => mockUsePermissions(),
}));

const sampleRegister: CashRegister = {
    id: '11111111-1111-1111-1111-111111111111',
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

beforeEach(() => {
    mockUsePermissions.mockReturnValue({ isSuperAdmin: false });
});

function renderActions(
    props: Partial<React.ComponentProps<typeof CashRegisterActions>> = {},
) {
    const onAction = vi.fn<(key: CashRegisterActionKey, register: CashRegister) => void>();
    render(
        <I18nProvider>
            <CashRegisterActions register={sampleRegister} onAction={onAction} {...props} />
        </I18nProvider>,
    );
    return { onAction };
}

describe('CashRegisterActions', () => {
    it('shows shift and daily closing actions for Manager', () => {
        renderActions();

        fireEvent.click(screen.getByRole('button', { name: /Aktionen/i }));

        expect(screen.getByText('Schicht öffnen')).toBeInTheDocument();
        expect(screen.getByText('Schicht schließen')).toBeInTheDocument();
        expect(screen.getByText('Tagesabschluss')).toBeInTheDocument();
        expect(screen.queryByText('Stilllegen')).not.toBeInTheDocument();
        expect(screen.queryByText('Löschen')).not.toBeInTheDocument();
    });

    it('shows lifecycle actions for Super Admin', () => {
        mockUsePermissions.mockReturnValue({ isSuperAdmin: true });
        renderActions();

        fireEvent.click(screen.getByRole('button', { name: /Aktionen/i }));

        expect(screen.getByText('Bearbeiten')).toBeInTheDocument();
        expect(screen.getByText('Löschen')).toBeInTheDocument();
        expect(screen.getByText('Stilllegen')).toBeInTheDocument();
    });

    it('calls onAction when a menu item is selected', () => {
        const { onAction } = renderActions();

        fireEvent.click(screen.getByRole('button', { name: /Aktionen/i }));
        fireEvent.click(screen.getByText('Tagesabschluss'));

        expect(onAction).toHaveBeenCalledWith('daily-closing', sampleRegister);
    });
});
