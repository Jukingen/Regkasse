'use client';

import { useEffect } from 'react';
import type { KeyboardShortcutEventName } from '@/shared/keyboardShortcuts';

/**
 * Subscribe to a FA keyboard-shortcut CustomEvent dispatched by `useKeyboardShortcuts`.
 */
export function useKeyboardShortcutListener<TDetail = unknown>(
    eventName: KeyboardShortcutEventName,
    handler: (detail: TDetail | undefined) => void,
    enabled = true,
): void {
    useEffect(() => {
        if (!enabled) return;

        const onEvent = (event: Event) => {
            const custom = event as CustomEvent<TDetail>;
            handler(custom.detail);
        };

        document.addEventListener(eventName, onEvent);
        return () => document.removeEventListener(eventName, onEvent);
    }, [eventName, handler, enabled]);
}
