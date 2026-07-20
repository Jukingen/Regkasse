'use client';

import { useKeyboardShortcut } from '@/hooks/useKeyboardShortcut';

/** @deprecated Prefer global `useKeyboardShortcuts` (Ctrl/Cmd+K → openSearch event). */
export function useCommandPaletteKeyboard(onOpen: () => void, enabled = true): void {
    useKeyboardShortcut('k', {
        metaOrCtrl: true,
        onTrigger: onOpen,
        enabled,
    });
}
