import Fuse, { type IFuseOptions } from 'fuse.js';
import type { CommandItem } from '@/features/command-palette/types';

const FUSE_OPTIONS: IFuseOptions<CommandItem> = {
    keys: [
        { name: 'label', weight: 0.5 },
        { name: 'description', weight: 0.25 },
        { name: 'keywords', weight: 0.25 },
    ],
    threshold: 0.3,
    includeScore: true,
    ignoreLocation: true,
};

/** Fuzzy filter for static / catalog commands (excludes `dynamic` placeholders). */
export function fuseSearchCommandItems(
    commands: readonly CommandItem[],
    searchTerm: string,
): CommandItem[] {
    const searchable = commands.filter((item) => !item.dynamic);
    const q = searchTerm.trim();
    if (!q) return searchable;

    const fuse = new Fuse(searchable, FUSE_OPTIONS);
    return fuse.search(q).map((result) => result.item);
}
