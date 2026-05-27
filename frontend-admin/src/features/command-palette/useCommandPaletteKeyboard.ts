'use client';

import { useKeyboardShortcut } from '@/hooks/useKeyboardShortcut';

/** @deprecated Prefer `useKeyboardShortcut('k', { metaOrCtrl: true, onTrigger })` from `@/hooks/useKeyboardShortcut`. */
export function useCommandPaletteKeyboard(onOpen: () => void, enabled = true): void {
    useKeyboardShortcut('k', {
        metaOrCtrl: true,
        onTrigger: onOpen,
        enabled,
    });
}
