import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ConfirmDialog } from '@/components/ConfirmDialog';
import { I18nProvider } from '@/i18n';

function renderDialog(ui: React.ReactElement) {
    return render(<I18nProvider>{ui}</I18nProvider>);
}

describe('ConfirmDialog', () => {
    it('renders title, message, and default button labels', () => {
        renderDialog(
            <ConfirmDialog
                open
                title="Löschen?"
                message="Dieser Eintrag wird entfernt."
                onConfirm={() => undefined}
                onCancel={() => undefined}
            />,
        );

        expect(screen.getByText('Löschen?')).toBeTruthy();
        expect(screen.getByText('Dieser Eintrag wird entfernt.')).toBeTruthy();
        expect(screen.getByRole('button', { name: 'Bestätigen' })).toBeTruthy();
        expect(screen.getByRole('button', { name: 'Abbrechen' })).toBeTruthy();
    });

    it('calls onConfirm and onCancel', () => {
        const onConfirm = vi.fn();
        const onCancel = vi.fn();

        renderDialog(
            <ConfirmDialog
                open
                title="Bestätigung"
                message="Fortfahren?"
                onConfirm={onConfirm}
                onCancel={onCancel}
            />,
        );

        fireEvent.click(screen.getByRole('button', { name: 'Bestätigen' }));
        fireEvent.click(screen.getByRole('button', { name: 'Abbrechen' }));

        expect(onConfirm).toHaveBeenCalledTimes(1);
        expect(onCancel).toHaveBeenCalledTimes(1);
    });

    it('uses custom button labels when provided', () => {
        renderDialog(
            <ConfirmDialog
                open
                title="Titel"
                message="Nachricht"
                confirmText="Ja, löschen"
                cancelText="Nein"
                type="danger"
                onConfirm={() => undefined}
                onCancel={() => undefined}
            />,
        );

        expect(screen.getByRole('button', { name: 'Ja, löschen' })).toBeTruthy();
        expect(screen.getByRole('button', { name: 'Nein' })).toBeTruthy();
    });
});
