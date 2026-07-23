import { describe, expect, it } from 'vitest';

import type { PermissionAuditEntry } from '@/features/audit/api/permissionAudit';
import {
  applyClientPermissionAuditFilters,
  isCriticalPermissionAuditEntry,
  matchesPermissionAuditFreeText,
} from '../permissionAuditFilters';

function entry(partial: Partial<PermissionAuditEntry>): PermissionAuditEntry {
  return {
    id: '1',
    timestamp: '2026-07-22T12:00:00.000Z',
    actorUserId: 'u1',
    actorName: 'Admin',
    actorEmail: 'admin@example.com',
    action: 'updated',
    roleId: 'r1',
    roleName: 'Cashier',
    permissionKey: 'sale.view',
    oldValue: 'absent',
    newValue: 'allowed',
    reason: 'Manager request',
    ...partial,
  };
}

describe('permissionAuditFilters', () => {
  it('matches free text across key fields', () => {
    const e = entry({});
    expect(matchesPermissionAuditFreeText(e, 'sale.view')).toBe(true);
    expect(matchesPermissionAuditFreeText(e, 'cashier')).toBe(true);
    expect(matchesPermissionAuditFreeText(e, 'admin@')).toBe(true);
    expect(matchesPermissionAuditFreeText(e, 'manager request')).toBe(true);
    expect(matchesPermissionAuditFreeText(e, 'missing')).toBe(false);
  });

  it('flags critical permission removals and lifecycle', () => {
    expect(
      isCriticalPermissionAuditEntry(
        entry({ action: 'deleted', permissionKey: '', oldValue: 'defaults', newValue: 'absent' })
      )
    ).toBe(true);
    expect(
      isCriticalPermissionAuditEntry(
        entry({
          permissionKey: 'payment.manage',
          oldValue: 'allowed',
          newValue: 'absent',
        })
      )
    ).toBe(true);
    expect(isCriticalPermissionAuditEntry(entry({ permissionKey: 'sale.view' }))).toBe(false);
  });

  it('applies combined client filters', () => {
    const rows = [
      entry({ id: 'a', permissionKey: 'sale.view', actorUserId: 'u1' }),
      entry({
        id: 'b',
        permissionKey: 'backup.manage',
        actorUserId: 'u2',
        action: 'updated',
        oldValue: 'allowed',
        newValue: 'absent',
      }),
      entry({
        id: 'c',
        permissionKey: 'sale.view',
        actorUserId: 'u1',
        timestamp: '2020-01-01T00:00:00.000Z',
      }),
    ];
    const filtered = applyClientPermissionAuditFilters(rows, {
      onlyCritical: true,
      onlyActorUserId: undefined,
      search: 'backup',
    });
    expect(filtered.map((r) => r.id)).toEqual(['b']);
  });
});
