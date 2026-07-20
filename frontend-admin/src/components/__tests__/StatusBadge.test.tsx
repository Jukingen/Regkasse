import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { StatusBadge } from '@/components/StatusBadge';
import { I18nProvider } from '@/i18n';

function renderBadge(ui: React.ReactElement) {
    return render(<I18nProvider>{ui}</I18nProvider>);
}

describe('StatusBadge', () => {
    it('renders localized label for active', () => {
        renderBadge(<StatusBadge status="active" />);
        expect(screen.getByText('Aktiv')).toBeTruthy();
    });

    it('prefers custom label over default', () => {
        renderBadge(<StatusBadge status="pending" label="Wartet auf Freigabe" />);
        expect(screen.getByText('Wartet auf Freigabe')).toBeTruthy();
    });

    it('renders pending with default label', () => {
        renderBadge(<StatusBadge status="pending" />);
        expect(screen.getByText('Ausstehend')).toBeTruthy();
    });

    it('renders suspended status', () => {
        renderBadge(<StatusBadge status="suspended" />);
        expect(screen.getByText('Gesperrt')).toBeTruthy();
    });
});
