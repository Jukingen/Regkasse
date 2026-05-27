import type { CommandItem } from '@/features/command-palette/types';

export const COMMAND_PALETTE_RECENT_STORAGE_KEY = 'fa_command_palette_recent_v1';
export const COMMAND_PALETTE_MAX_RECENT = 5;

export type RecentCommandSnapshot = {
    id: string;
    label: string;
    description?: string;
    type: CommandItem['type'];
};

function isBrowser(): boolean {
    return typeof window !== 'undefined' && typeof window.localStorage !== 'undefined';
}

export function snapshotCommandItem(item: CommandItem): RecentCommandSnapshot {
    return {
        id: item.id,
        label: item.label,
        description: item.description,
        type: item.type,
    };
}

export function readRecentCommandSnapshots(): RecentCommandSnapshot[] {
    if (!isBrowser()) return [];
    try {
        const raw = window.localStorage.getItem(COMMAND_PALETTE_RECENT_STORAGE_KEY);
        if (!raw) return [];
        const parsed = JSON.parse(raw) as RecentCommandSnapshot[];
        if (!Array.isArray(parsed)) return [];
        return parsed
            .filter((row) => row && typeof row.id === 'string' && typeof row.label === 'string')
            .slice(0, COMMAND_PALETTE_MAX_RECENT);
    } catch {
        return [];
    }
}

/** Persist last-used command (stores serializable snapshot, not `action`). */
export function storeRecentCommand(item: CommandItem): void {
    if (!isBrowser() || item.dynamic) return;
    const snapshot = snapshotCommandItem(item);
    const recent = readRecentCommandSnapshots();
    const filtered = recent.filter((c) => c.id !== snapshot.id);
    const updated = [snapshot, ...filtered].slice(0, COMMAND_PALETTE_MAX_RECENT);
    window.localStorage.setItem(COMMAND_PALETTE_RECENT_STORAGE_KEY, JSON.stringify(updated));
}

export function resolveRecentCommandItems(
    snapshots: RecentCommandSnapshot[],
    commandById: ReadonlyMap<string, CommandItem>,
    recreate: (snapshot: RecentCommandSnapshot) => CommandItem | null,
): CommandItem[] {
    const out: CommandItem[] = [];
    for (const snapshot of snapshots) {
        const live = commandById.get(snapshot.id);
        const item = live ?? recreate(snapshot);
        if (!item) continue;
        out.push({ ...item, group: 'Recent' });
    }
    return out;
}
