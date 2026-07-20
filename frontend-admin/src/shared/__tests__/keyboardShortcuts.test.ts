import { describe, expect, it } from 'vitest';
import {
    formatNavigateTabsShortcutLabel,
    formatShortcutLabel,
    isEditableTarget,
    matchesShortcut,
} from '@/shared/keyboardShortcuts';

describe('keyboardShortcuts helpers', () => {
    describe('matchesShortcut', () => {
        it('matches Ctrl+K', () => {
            expect(
                matchesShortcut(
                    { key: 'k', ctrlKey: true, metaKey: false, shiftKey: false, altKey: false },
                    { key: 'k', ctrl: true },
                ),
            ).toBe(true);
        });

        it('matches Cmd+K via metaKey', () => {
            expect(
                matchesShortcut(
                    { key: 'k', ctrlKey: false, metaKey: true, shiftKey: false, altKey: false },
                    { key: 'k', ctrl: true },
                ),
            ).toBe(true);
        });

        it('does not match Ctrl+Shift+K for Ctrl+K', () => {
            expect(
                matchesShortcut(
                    { key: 'k', ctrlKey: true, metaKey: false, shiftKey: true, altKey: false },
                    { key: 'k', ctrl: true },
                ),
            ).toBe(false);
        });

        it('matches Ctrl+Shift+L', () => {
            expect(
                matchesShortcut(
                    { key: 'l', ctrlKey: true, metaKey: false, shiftKey: true, altKey: false },
                    { key: 'l', ctrl: true, shift: true },
                ),
            ).toBe(true);
        });

        it('matches Escape without modifiers', () => {
            expect(
                matchesShortcut(
                    { key: 'Escape', ctrlKey: false, metaKey: false, shiftKey: false, altKey: false },
                    { key: 'Escape' },
                ),
            ).toBe(true);
        });

        it('is case-insensitive for letter keys', () => {
            expect(
                matchesShortcut(
                    { key: 'K', ctrlKey: true, metaKey: false, shiftKey: false, altKey: false },
                    { key: 'k', ctrl: true },
                ),
            ).toBe(true);
        });
    });

    describe('isEditableTarget', () => {
        it('detects input and textarea', () => {
            expect(isEditableTarget(document.createElement('input'))).toBe(true);
            expect(isEditableTarget(document.createElement('textarea'))).toBe(true);
            expect(isEditableTarget(document.createElement('select'))).toBe(true);
            expect(isEditableTarget(document.createElement('button'))).toBe(false);
        });
    });

    describe('formatShortcutLabel', () => {
        it('formats Ctrl+S style labels on non-Mac', () => {
            const original = navigator.platform;
            Object.defineProperty(navigator, 'platform', { value: 'Win32', configurable: true });
            expect(formatShortcutLabel({ ctrl: true, key: 's' })).toBe('Ctrl+S');
            expect(formatShortcutLabel({ ctrl: true, shift: true, key: 'l' })).toBe('Ctrl+Shift+L');
            expect(formatShortcutLabel({ key: 'Escape' })).toBe('Esc');
            expect(formatNavigateTabsShortcutLabel()).toBe('Ctrl+1–9');
            Object.defineProperty(navigator, 'platform', { value: original, configurable: true });
        });
    });
});
