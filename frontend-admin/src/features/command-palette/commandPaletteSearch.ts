import type { CommandItem, CommandItemGroup } from '@/features/command-palette/types';

export { fuseSearchCommandItems } from '@/features/command-palette/fuseCommandSearch';

const GROUP_SORT: Record<CommandItemGroup, number> = {
    Recent: 0,
    Actions: 1,
    Receipts: 2,
    Users: 3,
    Registers: 4,
    Navigation: 5,
};

function resolveGroup(item: CommandItem): CommandItemGroup {
    if (item.group) return item.group;
    switch (item.type) {
        case 'action':
            return 'Actions';
        case 'user':
            return 'Users';
        case 'receipt':
            return 'Receipts';
        case 'register':
            return 'Registers';
        default:
            return 'Navigation';
    }
}

export function sortCommandItems(items: CommandItem[]): CommandItem[] {
    return [...items].sort((a, b) => {
        const g = GROUP_SORT[resolveGroup(a)] - GROUP_SORT[resolveGroup(b)];
        if (g !== 0) return g;
        return a.label.localeCompare(b.label, 'de');
    });
}

/** @deprecated Use fuse search; kept for lightweight tests. */
export function looksLikeReceiptNumber(query: string): boolean {
    const q = query.trim();
    if (q.length < 3) return false;
    return /^[A-Za-z0-9][A-Za-z0-9._\-/]{2,}$/.test(q);
}
