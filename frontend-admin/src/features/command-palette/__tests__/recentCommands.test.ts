import { beforeEach, describe, expect, it } from 'vitest';

import {
  COMMAND_PALETTE_RECENT_STORAGE_KEY,
  readRecentCommandSnapshots,
  storeRecentCommand,
} from '@/features/command-palette/recentCommands';
import type { CommandItem } from '@/features/command-palette/types';

const sample: CommandItem = {
  id: 'page:dashboard',
  type: 'page',
  label: 'Dashboard',
  keywords: [],
  action: () => {},
};

describe('recentCommands', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('stores up to 5 recent command snapshots by id', () => {
    storeRecentCommand(sample);
    storeRecentCommand({
      ...sample,
      id: 'page:users',
      label: 'Benutzer',
    });
    const recent = readRecentCommandSnapshots();
    expect(recent).toHaveLength(2);
    expect(recent[0].id).toBe('page:users');
  });

  it('moves duplicate to front', () => {
    storeRecentCommand(sample);
    storeRecentCommand({ ...sample, id: 'page:users', label: 'Benutzer' });
    storeRecentCommand(sample);
    const recent = readRecentCommandSnapshots();
    expect(recent[0].id).toBe('page:dashboard');
    expect(recent).toHaveLength(2);
  });

  it('persists to localStorage', () => {
    storeRecentCommand(sample);
    const raw = window.localStorage.getItem(COMMAND_PALETTE_RECENT_STORAGE_KEY);
    expect(raw).toContain('page:dashboard');
  });
});
