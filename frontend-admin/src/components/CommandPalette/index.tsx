'use client';

import { useCallback, useState } from 'react';
import { CommandPalette } from '@/components/CommandPalette/CommandPalette';
import { useKeyboardShortcut } from '@/hooks/useKeyboardShortcut';

/**
 * Global command palette: Cmd+K / Ctrl+K on all protected pages.
 * Mount once in `(protected)/layout.tsx`.
 */
export function CommandPaletteShell() {
    const [open, setOpen] = useState(false);

    const openPalette = useCallback(() => setOpen(true), []);
    const closePalette = useCallback(() => setOpen(false), []);

    useKeyboardShortcut('k', {
        metaOrCtrl: true,
        onTrigger: openPalette,
    });

    return <CommandPalette open={open} onClose={closePalette} />;
}

export { CommandPalette } from '@/components/CommandPalette/CommandPalette';
export type { CommandItem, CommandItemType, CommandItemGroup } from '@/components/CommandPalette/types';
export { useCommands } from '@/components/CommandPalette/useCommands';
export { useCommandRegistry } from '@/components/CommandPalette/commandRegistry';
