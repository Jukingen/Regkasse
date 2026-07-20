import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { EmptyState } from '@/components/EmptyState';
import { I18nProvider } from '@/i18n';

function renderEmpty(ui: React.ReactElement) {
    return render(<I18nProvider>{ui}</I18nProvider>);
}

describe('EmptyState', () => {
    it('renders default localized title and description', () => {
        renderEmpty(<EmptyState />);
        expect(screen.getByText('Keine Daten gefunden')).toBeTruthy();
        expect(screen.getByText('Es wurden noch keine Einträge erstellt.')).toBeTruthy();
    });

    it('renders custom title and description', () => {
        renderEmpty(
            <EmptyState title="Keine Produkte" description="Katalog ist leer." />,
        );
        expect(screen.getByText('Keine Produkte')).toBeTruthy();
        expect(screen.getByText('Katalog ist leer.')).toBeTruthy();
    });

    it('renders action button and calls onAction', () => {
        const onAction = vi.fn();
        renderEmpty(
            <EmptyState actionText="Neu anlegen" onAction={onAction} />,
        );
        fireEvent.click(screen.getByRole('button', { name: 'Neu anlegen' }));
        expect(onAction).toHaveBeenCalledTimes(1);
    });

    it('does not render action when only actionText is set', () => {
        renderEmpty(<EmptyState actionText="Neu anlegen" />);
        expect(screen.queryByRole('button', { name: 'Neu anlegen' })).toBeNull();
    });
});
