import React from 'react';
import { describe, it, expect, beforeAll, vi } from 'vitest';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nProvider } from '@/i18n';
import { SelectedCashRegisterCard } from '@/components/SelectedCashRegisterCard';

vi.mock('@/features/license/hooks/useLicense', () => ({
    useLicense: () => ({ licenseStatus: null }),
}));

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

function renderCard(showAutoSelectedTag = true) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return render(
        <QueryClientProvider client={client}>
            <I18nProvider>
                <SelectedCashRegisterCard
                    showAutoSelectedTag={showAutoSelectedTag}
                    register={{
                        id: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
                        tenantId: 'tenant-a',
                        registerNumber: 'KASSE-001',
                        location: 'Theke',
                        status: 2,
                        tseHealthStatus: 'healthy',
                        lastSyncAtUtc: '2026-07-10T10:00:00Z',
                    }}
                />
            </I18nProvider>
        </QueryClientProvider>,
    );
}

describe('SelectedCashRegisterCard', () => {
    it('renders prominent active context for auto-selected register', () => {
        renderCard(true);

        expect(screen.getByTestId('selected-cash-register-card')).toBeInTheDocument();
        expect(screen.getByText('Aktuelle Kasse')).toBeInTheDocument();
        expect(screen.getByText(/KASSE-001/)).toBeInTheDocument();
        expect(screen.getByText('Aktiv')).toBeInTheDocument();
        expect(screen.getByText('Automatisch ausgewählt')).toBeInTheDocument();
        expect(screen.getByText(/ID: aaaaaaaa/)).toBeInTheDocument();
        expect(screen.getByText(/Diese Kasse wurde automatisch ausgewählt/)).toBeInTheDocument();
    });

    it('renders selected tag when not auto-selected', () => {
        renderCard(false);

        expect(screen.getByText('Ausgewählt')).toBeInTheDocument();
        expect(screen.queryByText('Automatisch ausgewählt')).not.toBeInTheDocument();
        expect(screen.queryByText(/Diese Kasse wurde automatisch ausgewählt/)).not.toBeInTheDocument();
    });
});
