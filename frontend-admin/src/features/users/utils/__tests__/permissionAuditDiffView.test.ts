import { describe, expect, it } from 'vitest';

import {
  linesFromDedicatedEntry,
  summarizePermissionDiffLines,
  toPermissionDiffTableRows,
} from '../permissionAuditDiffView';

describe('permissionAuditDiffView', () => {
  it('summarizes added/removed/changed', () => {
    const summary = summarizePermissionDiffLines([
      { permissionKey: 'a', change: 'added', oldState: 'absent', newState: 'allowed' },
      { permissionKey: 'b', change: 'removed', oldState: 'allowed', newState: 'absent' },
      { permissionKey: 'c', change: 'changed', oldState: 'denied', newState: 'individual' },
    ]);
    expect(summary).toEqual({ added: 1, removed: 1, changed: 1 });
  });

  it('builds table rows from a dedicated entry', () => {
    const lines = linesFromDedicatedEntry({
      id: '1',
      timestamp: '2026-07-22T12:00:00Z',
      actorUserId: 'u',
      actorName: 'Admin',
      actorEmail: 'a@b.c',
      action: 'updated',
      roleId: 'r',
      roleName: 'Cashier',
      permissionKey: 'dailyclosing.view',
      oldValue: 'allowed',
      newValue: 'individual',
    });
    const rows = toPermissionDiffTableRows(lines);
    expect(rows).toHaveLength(1);
    expect(rows[0]?.marker).toBe('yellow');
    expect(rows[0]?.permissionKey).toBe('dailyclosing.view');
  });
});
