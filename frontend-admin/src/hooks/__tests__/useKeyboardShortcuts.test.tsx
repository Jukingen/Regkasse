import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useKeyboardShortcuts } from '@/hooks/useKeyboardShortcuts';
import { KEYBOARD_SHORTCUT_EVENTS } from '@/shared/keyboardShortcuts';

const mockPush = vi.fn();
const mockLogout = vi.fn();
const mockConfirm = vi.fn();
const mockT = vi.fn((key: string) => key);

vi.mock('next/navigation', () => ({
    useRouter: () => ({ push: mockPush }),
    usePathname: () => '/admin/tenants',
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
    useAuth: () => ({ logout: mockLogout }),
}));

vi.mock('@/hooks/useAntdApp', () => ({
    useAntdApp: () => ({ modal: { confirm: mockConfirm } }),
}));

vi.mock('@/i18n', () => ({
    useI18n: () => ({ t: mockT }),
}));

describe('useKeyboardShortcuts', () => {
    beforeEach(() => {
        mockPush.mockReset();
        mockLogout.mockReset();
        mockConfirm.mockReset();
        mockT.mockImplementation((key: string) => key);
    });

    afterEach(() => {
        vi.clearAllMocks();
    });

    function dispatchKey(init: KeyboardEventInit) {
        const event = new KeyboardEvent('keydown', { bubbles: true, ...init });
        let prevented = false;
        Object.defineProperty(event, 'preventDefault', {
            value: () => {
                prevented = true;
            },
        });
        document.body.dispatchEvent(event);
        return prevented;
    }

    it('dispatches openSearch on Ctrl+K', () => {
        const onOpen = vi.fn();
        document.addEventListener(KEYBOARD_SHORTCUT_EVENTS.openSearch, onOpen);

        renderHook(() => useKeyboardShortcuts());
        const prevented = dispatchKey({ key: 'k', ctrlKey: true });

        expect(prevented).toBe(true);
        expect(onOpen).toHaveBeenCalledTimes(1);
        document.removeEventListener(KEYBOARD_SHORTCUT_EVENTS.openSearch, onOpen);
    });

    it('navigates to create tenant on Ctrl+N from tenants list', () => {
        renderHook(() => useKeyboardShortcuts());
        dispatchKey({ key: 'n', ctrlKey: true });
        expect(mockPush).toHaveBeenCalledWith('/admin/tenants/create');
    });

    it('dispatches navigateTab on Ctrl+2', () => {
        const onTab = vi.fn();
        document.addEventListener(KEYBOARD_SHORTCUT_EVENTS.navigateTab, onTab as EventListener);

        renderHook(() => useKeyboardShortcuts());
        dispatchKey({ key: '2', ctrlKey: true });

        expect(onTab).toHaveBeenCalledTimes(1);
        const event = onTab.mock.calls[0][0] as CustomEvent<{ index: number }>;
        expect(event.detail).toEqual({ index: 1 });
        document.removeEventListener(KEYBOARD_SHORTCUT_EVENTS.navigateTab, onTab as EventListener);
    });

    it('ignores Ctrl+K when focus is in an input', () => {
        const onOpen = vi.fn();
        document.addEventListener(KEYBOARD_SHORTCUT_EVENTS.openSearch, onOpen);

        const input = document.createElement('input');
        document.body.appendChild(input);
        input.focus();

        renderHook(() => useKeyboardShortcuts());
        input.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', ctrlKey: true, bubbles: true }));

        expect(onOpen).not.toHaveBeenCalled();
        document.body.removeChild(input);
        document.removeEventListener(KEYBOARD_SHORTCUT_EVENTS.openSearch, onOpen);
    });

    it('allows Ctrl+S in inputs and dispatches triggerSave', () => {
        const onSave = vi.fn();
        document.addEventListener(KEYBOARD_SHORTCUT_EVENTS.triggerSave, onSave);

        const input = document.createElement('input');
        document.body.appendChild(input);
        input.focus();

        renderHook(() => useKeyboardShortcuts());
        input.dispatchEvent(new KeyboardEvent('keydown', { key: 's', ctrlKey: true, bubbles: true }));

        expect(onSave).toHaveBeenCalledTimes(1);
        document.body.removeChild(input);
        document.removeEventListener(KEYBOARD_SHORTCUT_EVENTS.triggerSave, onSave);
    });

    it('opens logout confirm on Ctrl+Shift+L', () => {
        renderHook(() => useKeyboardShortcuts());
        act(() => {
            dispatchKey({ key: 'l', ctrlKey: true, shiftKey: true });
        });
        expect(mockConfirm).toHaveBeenCalledTimes(1);
    });

    it('returns shortcut labels', () => {
        const { result } = renderHook(() => useKeyboardShortcuts());
        expect(result.current.getShortcutLabel('openSearch')).toMatch(/K/);
        expect(result.current.getShortcutLabel('logout')).toMatch(/L/);
    });
});
