'use client';

import { useCallback, useState } from 'react';
import { CommandPalette } from '@/components/CommandPalette/CommandPalette';

/**
 * Global command palette modal (entity search, actions).
 * Menu search shortcut (Ctrl+K / Cmd+K) is owned by `GlobalSearch` in the shell header.
 * Mount once in `(protected)/layout.tsx`.
 */
export function CommandPaletteShell() {
    const [open, setOpen] = useState(false);

    const closePalette = useCallback(() => setOpen(false), []);

    return <CommandPalette open={open} onClose={closePalette} />;
}

export { CommandPalette } from '@/components/CommandPalette/CommandPalette';
export type { CommandItem, CommandItemType, CommandItemGroup } from '@/components/CommandPalette/types';
export { useCommands } from '@/components/CommandPalette/useCommands';
export { useCommandRegistry } from '@/components/CommandPalette/commandRegistry';
