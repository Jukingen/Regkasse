'use client';

import { useEffect } from 'react';

export type UseKeyboardShortcutOptions = {
    /** Require Meta (Mac) or Ctrl (Windows/Linux). */
    metaOrCtrl?: boolean;
    /** Require Shift. */
    shift?: boolean;
    /** Require Alt. */
    alt?: boolean;
    onTrigger: () => void;
    enabled?: boolean;
    /** When true, ignore shortcuts while focus is in inputs (default true). */
    ignoreInputs?: boolean;
};

function isEditableTarget(target: EventTarget | null): boolean {
    if (!(target instanceof HTMLElement)) return false;
    const tag = target.tagName;
    return (
        tag === 'INPUT' ||
        tag === 'TEXTAREA' ||
        tag === 'SELECT' ||
        Boolean(target.isContentEditable)
    );
}

/**
 * Global keydown listener (e.g. Cmd/Ctrl+K for command palette).
 */
export function useKeyboardShortcut(key: string, options: UseKeyboardShortcutOptions): void {
    const {
        metaOrCtrl = false,
        shift = false,
        alt = false,
        onTrigger,
        enabled = true,
        ignoreInputs = true,
    } = options;

    useEffect(() => {
        if (!enabled) return;

        const handleKeyDown = (e: KeyboardEvent) => {
            if (e.key.toLowerCase() !== key.toLowerCase()) return;
            if (metaOrCtrl && !(e.metaKey || e.ctrlKey)) return;
            if (shift && !e.shiftKey) return;
            if (alt && !e.altKey) return;
            if (ignoreInputs && isEditableTarget(e.target)) return;

            e.preventDefault();
            onTrigger();
        };

        window.addEventListener('keydown', handleKeyDown);
        return () => window.removeEventListener('keydown', handleKeyDown);
    }, [key, metaOrCtrl, shift, alt, onTrigger, enabled, ignoreInputs]);
}
