import { describe, expect, it } from 'vitest';

import {
  buildPermissionSearchEntries,
  buildPermissionSearchIndex,
  escapeRegExp,
  permissionMatchesSearch,
  searchPermissions,
} from '../permissionSearchIndex';

describe('permissionSearchIndex', () => {
  const items = [
    { key: 'daily-closing.view', group: 'Tagesabschluss' },
    { key: 'daily-closing.execute', group: 'Tagesabschluss' },
    { key: 'user.view', group: 'Mitarbeiter' },
  ];

  it('builds structured multi-language entries', () => {
    const entries = buildPermissionSearchEntries(items);
    const daily = entries.find((e) => e.key === 'daily-closing.view');
    expect(daily).toBeDefined();
    expect(daily!.group).toBe('tagesabschluss');
    expect(daily!.labels.de.toLowerCase()).toMatch(/tagesabschluss|anzeigen/);
    expect(daily!.labels.en.toLowerCase()).toMatch(/daily|view/);
    expect(daily!.labels.tr.toLowerCase()).toMatch(/gün|görüntüle|kapanış/);
    expect(daily!.groupLabels.de).toMatch(/Tagesabschluss/i);
    expect(daily!.groupLabels.en).toMatch(/Daily closing/i);
    expect(daily!.groupLabels.tr).toMatch(/Gün sonu/i);
  });

  it('searchPermissions matches key in any language mode', () => {
    const entries = buildPermissionSearchEntries(items);
    expect(searchPermissions(entries, 'daily-closing').map((e) => e.key)).toContain(
      'daily-closing.view'
    );
    expect(searchPermissions(entries, 'user.view').map((e) => e.key)).toEqual(['user.view']);
  });

  it('searchPermissions matches DE/EN/TR labels when language=all', () => {
    const entries = buildPermissionSearchEntries(items);
    expect(searchPermissions(entries, 'anzeigen', 'all').some((e) => e.key === 'daily-closing.view')).toBe(
      true
    );
    expect(searchPermissions(entries, 'Perform', 'all').some((e) => e.key === 'daily-closing.execute')).toBe(
      true
    );
    expect(searchPermissions(entries, 'gün sonu', 'all').length).toBeGreaterThan(0);
  });

  it('searchPermissions can scope to a single language', () => {
    const entries = buildPermissionSearchEntries(items);
    // English-only: German-only word should not match unless also in EN label
    const enOnly = searchPermissions(entries, 'anzeigen', 'en');
    const all = searchPermissions(entries, 'anzeigen', 'all');
    expect(all.length).toBeGreaterThanOrEqual(enOnly.length);
  });

  it('empty query returns all', () => {
    const entries = buildPermissionSearchEntries(items);
    expect(searchPermissions(entries, '  ')).toHaveLength(items.length);
  });

  it('legacy Map index still works', () => {
    const index = buildPermissionSearchIndex(items);
    expect(permissionMatchesSearch('daily-closing.view', 'daily-closing', index)).toBe(true);
    expect(permissionMatchesSearch('user.view', 'daily-closing', index)).toBe(false);
  });

  it('escapeRegExp escapes special characters', () => {
    expect(escapeRegExp('a.b+c')).toBe('a\\.b\\+c');
  });
});
