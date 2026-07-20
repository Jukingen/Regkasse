import { describe, expect, it, vi, beforeEach } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { SaveButton } from '@/components/SaveButton';
import { KEYBOARD_SHORTCUT_EVENTS } from '@/shared/keyboardShortcuts';

vi.mock('@/i18n', () => ({
    useI18n: () => ({
        t: (key: string, params?: { shortcut?: string }) => {
            if (key === 'keyboardShortcuts.saveWithShortcut') {
                return `Save (${params?.shortcut ?? 'Ctrl+S'})`;
            }
            if (key === 'common.buttons.save') return 'Save';
            return key;
        },
    }),
}));

vi.mock('@/components/KeyboardShortcutsProvider', () => ({
    useKeyboardShortcutLabels: () => ({
        getShortcutLabel: () => 'Ctrl+S',
    }),
}));

describe('SaveButton', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('renders shortcut label by default', () => {
        render(<SaveButton onClick={vi.fn()} />);
        expect(screen.getByRole('button', { name: /Save \(Ctrl\+S\)/i })).toBeTruthy();
    });

    it('calls onClick on button press', () => {
        const onClick = vi.fn();
        render(<SaveButton onClick={onClick} />);
        fireEvent.click(screen.getByRole('button'));
        expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('triggers onClick via regkasse:triggerSave', () => {
        const onClick = vi.fn();
        render(<SaveButton onClick={onClick} />);
        document.dispatchEvent(new CustomEvent(KEYBOARD_SHORTCUT_EVENTS.triggerSave));
        expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('ignores shortcut when disabled or loading', () => {
        const onClick = vi.fn();
        const { rerender } = render(<SaveButton onClick={onClick} disabled />);
        document.dispatchEvent(new CustomEvent(KEYBOARD_SHORTCUT_EVENTS.triggerSave));
        expect(onClick).not.toHaveBeenCalled();

        rerender(<SaveButton onClick={onClick} loading />);
        document.dispatchEvent(new CustomEvent(KEYBOARD_SHORTCUT_EVENTS.triggerSave));
        expect(onClick).not.toHaveBeenCalled();
    });

    it('ignores shortcut when shortcutEnabled is false', () => {
        const onClick = vi.fn();
        render(<SaveButton onClick={onClick} shortcutEnabled={false} />);
        document.dispatchEvent(new CustomEvent(KEYBOARD_SHORTCUT_EVENTS.triggerSave));
        expect(onClick).not.toHaveBeenCalled();
    });
});
