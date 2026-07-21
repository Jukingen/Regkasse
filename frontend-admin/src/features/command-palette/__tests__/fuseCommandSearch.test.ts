import { describe, expect, it } from 'vitest';

import { fuseSearchCommandItems } from '@/features/command-palette/fuseCommandSearch';
import type { CommandItem } from '@/features/command-palette/types';

const items: CommandItem[] = [
  {
    id: 'page:users',
    type: 'page',
    label: 'Benutzer',
    description: '/admin/users',
    keywords: ['users', 'accounts'],
    action: () => {},
  },
  {
    id: 'page:backup',
    type: 'page',
    label: 'Backup & DR',
    keywords: ['backup', 'restore'],
    action: () => {},
  },
];

describe('fuseSearchCommandItems', () => {
  it('returns all items when search is empty', () => {
    expect(fuseSearchCommandItems(items, '')).toHaveLength(2);
  });

  it('fuzzy-matches label and keywords', () => {
    const results = fuseSearchCommandItems(items, 'acounts');
    expect(results.some((r) => r.id === 'page:users')).toBe(true);
  });

  it('matches backup keyword', () => {
    const results = fuseSearchCommandItems(items, 'restore');
    expect(results.some((r) => r.id === 'page:backup')).toBe(true);
  });
});
