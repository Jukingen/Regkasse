import { describe, expect, it } from 'vitest';

import {
  buildPermissionAuditDiff,
  permissionAuditBorderColor,
  permissionAuditTagColor,
} from '../permissionAuditDiff';

describe('permissionAuditDiff', () => {
  it('diffs ROLE_PERMISSIONS_UPDATE into added and removed lines', () => {
    const diff = buildPermissionAuditDiff({
      action: 'ROLE_PERMISSIONS_UPDATE',
      oldValues: JSON.stringify({
        roleName: 'Cashier',
        permissions: ['cashregister.manage', 'sale.view'],
      }),
      newValues: JSON.stringify({
        roleName: 'Cashier',
        permissions: ['sale.view', 'dailyclosing.view'],
      }),
    });

    expect(diff.roleName).toBe('Cashier');
    expect(diff.color).toBe('yellow');
    expect(diff.lines).toEqual([
      {
        permissionKey: 'dailyclosing.view',
        change: 'added',
        oldState: 'absent',
        newState: 'allowed',
      },
      {
        permissionKey: 'cashregister.manage',
        change: 'removed',
        oldState: 'allowed',
        newState: 'absent',
      },
    ]);
  });

  it('colors only-added updates green', () => {
    const diff = buildPermissionAuditDiff({
      action: 'ROLE_PERMISSIONS_UPDATE',
      oldValues: JSON.stringify({ roleName: 'X', permissions: ['a'] }),
      newValues: JSON.stringify({ roleName: 'X', permissions: ['a', 'b'] }),
    });
    expect(diff.color).toBe('green');
    expect(diff.lines).toHaveLength(1);
    expect(diff.lines[0]?.change).toBe('added');
  });

  it('colors only-removed updates red', () => {
    const diff = buildPermissionAuditDiff({
      action: 'ROLE_PERMISSIONS_UPDATE',
      oldValues: JSON.stringify({ roleName: 'X', permissions: ['a', 'b'] }),
      newValues: JSON.stringify({ roleName: 'X', permissions: ['a'] }),
    });
    expect(diff.color).toBe('red');
    expect(diff.lines[0]?.change).toBe('removed');
  });

  it('maps ROLE_CREATE / ROLE_DELETE as blue lifecycle', () => {
    const created = buildPermissionAuditDiff({
      action: 'ROLE_CREATE',
      newValues: JSON.stringify({ roleName: 'Kassierer', permissions: ['sale.view'] }),
    });
    expect(created.color).toBe('blue');
    expect(created.lines[0]).toMatchObject({
      permissionKey: 'Kassierer',
      change: 'lifecycle',
      newState: 'defaults',
    });

    const deleted = buildPermissionAuditDiff({
      action: 'ROLE_DELETE',
      oldValues: JSON.stringify({ roleName: 'Kassierer', permissions: [] }),
      entityName: 'Kassierer',
    });
    expect(deleted.color).toBe('blue');
    expect(deleted.lines[0]?.oldState).toBe('defaults');
  });

  it('maps override grant/deny/remove as changed states', () => {
    const granted = buildPermissionAuditDiff({
      action: 'USER_PERMISSION_OVERRIDES_CHANGED',
      oldValues: null,
      newValues: JSON.stringify({
        permission: 'report.view',
        isGranted: true,
        id: 'ov-1',
      }),
    });
    expect(granted.color).toBe('green');
    expect(granted.lines[0]).toMatchObject({
      permissionKey: 'report.view',
      change: 'added',
      oldState: 'absent',
      newState: 'individual',
    });

    const flipped = buildPermissionAuditDiff({
      action: 'USER_PERMISSION_OVERRIDES_CHANGED',
      oldValues: JSON.stringify({ permission: 'report.view', isGranted: false }),
      newValues: JSON.stringify({ permission: 'report.view', isGranted: true }),
    });
    expect(flipped.color).toBe('yellow');
    expect(flipped.lines[0]).toMatchObject({
      change: 'changed',
      oldState: 'denied',
      newState: 'individual',
    });

    const removed = buildPermissionAuditDiff({
      action: 'USER_PERMISSION_OVERRIDES_CHANGED',
      oldValues: JSON.stringify({ permission: 'report.view', isGranted: true }),
      newValues: JSON.stringify({ removed: true, overrideId: 'ov-1' }),
    });
    expect(removed.color).toBe('red');
    expect(removed.lines[0]?.change).toBe('removed');
  });

  it('maps color helpers to antd tokens and borders', () => {
    expect(permissionAuditTagColor('green')).toBe('success');
    expect(permissionAuditTagColor('red')).toBe('error');
    expect(permissionAuditTagColor('yellow')).toBe('warning');
    expect(permissionAuditTagColor('blue')).toBe('processing');
    expect(permissionAuditBorderColor('green')).toBe('#52c41a');
  });
});
