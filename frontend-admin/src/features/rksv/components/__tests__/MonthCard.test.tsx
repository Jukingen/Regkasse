import React from 'react';
import { describe, it, expect, vi, beforeAll, beforeEach } from 'vitest';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { I18nProvider } from '@/i18n';
import {
    MonthCard,
    type MonthCardAction,
    type MonthCardActionPayload,
} from '@/features/rksv/components/MonthCard';

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

function renderCard(
    props: Partial<React.ComponentProps<typeof MonthCard>> = {},
) {
    const onAction = vi.fn<(action: MonthCardAction, data: MonthCardActionPayload) => void>();
    const onOpenSummary = vi.fn<(data: MonthCardActionPayload) => void>();
    render(
        <I18nProvider>
            <MonthCard month={3} year={2026} status="missing" onAction={onAction} {...props} />
        </I18nProvider>,
    );
    return { onAction, onOpenSummary };
}

describe('MonthCard', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('opens action menu on click when no summary handler is set', async () => {
        const { onAction } = renderCard({ status: 'missing' });

        fireEvent.click(screen.getByRole('button', { name: /März|Mar|2026/i }));

        const createItem = await screen.findByText(/Monatsbeleg nachträglich erstellen/i);
        fireEvent.click(createItem);

        expect(onAction).toHaveBeenCalledWith(
            'create-late',
            expect.objectContaining({ month: 3, year: 2026, status: 'missing' }),
        );
    });

    it('opens summary on click when onOpenSummary is provided', () => {
        const onOpenSummary = vi.fn();
        renderCard({ status: 'missing', onOpenSummary });

        fireEvent.click(screen.getByRole('button', { name: /2026/i }));

        expect(onOpenSummary).toHaveBeenCalledWith(
            expect.objectContaining({ month: 3, year: 2026, status: 'missing' }),
        );
        expect(screen.queryByRole('menu')).not.toBeInTheDocument();
    });

    it('shows view report for completed months and recreate only for SuperAdmin', async () => {
        const { onAction } = renderCard({ status: 'completed', canRecreate: true });

        fireEvent.click(screen.getByRole('button', { name: /2026/i }));

        expect(await screen.findByText(/Monatsbeleg anzeigen/i)).toBeInTheDocument();
        expect(screen.getByText(/Erneut erstellen/i)).toBeInTheDocument();
        expect(screen.queryByText(/nachträglich erstellen/i)).not.toBeInTheDocument();

        fireEvent.click(screen.getByText(/Monatsbeleg anzeigen/i));
        expect(onAction).toHaveBeenCalledWith(
            'view-report',
            expect.objectContaining({ status: 'completed' }),
        );
    });

    it('hides recreate when canRecreate is false', async () => {
        renderCard({ status: 'completed', canRecreate: false });

        fireEvent.click(screen.getByRole('button', { name: /2026/i }));

        expect(await screen.findByText(/Monatsbeleg anzeigen/i)).toBeInTheDocument();
        expect(screen.queryByText(/Erneut erstellen/i)).not.toBeInTheDocument();
    });

    it('offers copy link from context menu', async () => {
        const { onAction } = renderCard({ status: 'pending' });
        const card = screen.getByRole('button', { name: /2026/i });

        fireEvent.contextMenu(card);

        const copyItem = await screen.findByText(/Link zum Monat kopieren/i);
        fireEvent.click(copyItem);

        expect(onAction).toHaveBeenCalledWith(
            'copy-link',
            expect.objectContaining({ month: 3, year: 2026, status: 'pending' }),
        );
    });

    it('exposes shared revenue and receipts actions for pending months', async () => {
        const { onAction } = renderCard({ status: 'pending' });

        fireEvent.click(screen.getByRole('button', { name: /2026/i }));

        const menu = await screen.findByRole('menu');
        expect(within(menu).getByText(/Umsatz für diesen Monat anzeigen/i)).toBeInTheDocument();
        expect(within(menu).getByText(/Belege für diesen Monat anzeigen/i)).toBeInTheDocument();
        expect(within(menu).queryByText(/nachträglich erstellen/i)).not.toBeInTheDocument();
        expect(within(menu).queryByText(/Monatsbeleg anzeigen/i)).not.toBeInTheDocument();

        fireEvent.click(within(menu).getByText(/Belege für diesen Monat anzeigen/i));
        expect(onAction).toHaveBeenCalledWith('view-receipts', expect.any(Object));
    });
});
