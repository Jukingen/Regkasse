/** Platform-aware menu search shortcut label for UI hints. */
export function getGlobalSearchShortcutLabel(): string {
    if (typeof navigator !== 'undefined' && /Mac|iPhone|iPad/i.test(navigator.platform)) {
        return '⌘K';
    }
    return 'Ctrl+K';
}

export function getGlobalSearchOptionId(listboxId: string, index: number): string {
    return `${listboxId}-option-${index}`;
}
