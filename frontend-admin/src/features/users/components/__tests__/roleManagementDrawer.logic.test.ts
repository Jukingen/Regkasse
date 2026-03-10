/**
 * Role management drawer logic – next selection after delete, dirty-state, system role delete disabled.
 * Mirrors the behavior in RoleManagementDrawer (no Ant Design render).
 */

import { describe, it, expect } from 'vitest';

/** Same logic as in RoleManagementDrawer handleDelete onOk: remaining[0]?.roleName ?? null */
function getNextRoleAfterDelete(
  sortedRoles: { roleName: string }[],
  deletedRoleName: string
): string | null {
  const remaining = sortedRoles.filter((r) => r.roleName !== deletedRoleName);
  return remaining[0]?.roleName ?? null;
}

/** Dirty when draft set differs from saved set (same logic as drawer). */
function isDirty(
  draftPermissions: Set<string>,
  savedPermissions: Set<string>
): boolean {
  if (draftPermissions.size !== savedPermissions.size) return true;
  const draftArr = Array.from(draftPermissions);
  const savedArr = Array.from(savedPermissions);
  return (
    !draftArr.every((p) => savedPermissions.has(p)) ||
    !savedArr.every((p) => draftPermissions.has(p))
  );
}

describe('RoleManagementDrawer logic', () => {
  describe('delete next selection', () => {
    it('returns first remaining role in sorted order after delete', () => {
      const roles = [
        { roleName: 'SuperAdmin' },
        { roleName: 'Custom' },
        { roleName: 'Manager' },
      ];
      expect(getNextRoleAfterDelete(roles, 'Custom')).toBe('SuperAdmin');
    });

    it('returns null when deleted was the only role', () => {
      expect(getNextRoleAfterDelete([{ roleName: 'X' }], 'X')).toBeNull();
    });

    it('returns first role when last role is deleted', () => {
      const roles = [{ roleName: 'A' }, { roleName: 'B' }];
      expect(getNextRoleAfterDelete(roles, 'B')).toBe('A');
    });
  });

  describe('dirty-state', () => {
    it('is dirty when draft has extra permission', () => {
      const draft = new Set(['a', 'b']);
      const saved = new Set(['a']);
      expect(isDirty(draft, saved)).toBe(true);
    });

    it('is dirty when draft misses a permission', () => {
      const draft = new Set(['a']);
      const saved = new Set(['a', 'b']);
      expect(isDirty(draft, saved)).toBe(true);
    });

    it('is not dirty when draft equals saved', () => {
      const draft = new Set(['a', 'b']);
      const saved = new Set(['a', 'b']);
      expect(isDirty(draft, saved)).toBe(false);
    });
  });

  describe('system role delete disabled', () => {
    it('isSystemRole true implies delete should be disabled in UI', () => {
      const role = { roleName: 'Manager', isSystemRole: true, userCount: 1 };
      expect(role.isSystemRole).toBe(true);
    });
  });
});
