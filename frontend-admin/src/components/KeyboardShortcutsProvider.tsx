'use client';

import { createContext, useContext, type ReactNode } from 'react';
import {
    useKeyboardShortcuts,
    type UseKeyboardShortcutsResult,
} from '@/hooks/useKeyboardShortcuts';

const KeyboardShortcutsContext = createContext<UseKeyboardShortcutsResult | null>(null);

/**
 * Mounts global FA keyboard shortcuts once in the protected shell.
 */
export function KeyboardShortcutsProvider({ children }: { children: ReactNode }) {
    const value = useKeyboardShortcuts();
    return (
        <KeyboardShortcutsContext.Provider value={value}>
            {children}
        </KeyboardShortcutsContext.Provider>
    );
}

export function useKeyboardShortcutLabels(): UseKeyboardShortcutsResult {
    const ctx = useContext(KeyboardShortcutsContext);
    if (!ctx) {
        return {
            getShortcutLabel: () => '',
        };
    }
    return ctx;
}
