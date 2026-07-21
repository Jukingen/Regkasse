import { describe, expect, it } from 'vitest';

import {
  PERMISSIONS,
  hasAllPermissions,
  hasAnyPermission,
  hasPermission,
} from '@/shared/auth/permissions';

describe('permissions helpers', () => {
  it('hasPermission returns false for null/empty permission lists', () => {
    expect(hasPermission(null, PERMISSIONS.USER_VIEW)).toBe(false);
    expect(hasPermission(undefined, PERMISSIONS.USER_VIEW)).toBe(false);
    expect(hasPermission({ permissions: [] }, PERMISSIONS.USER_VIEW)).toBe(false);
    expect(hasPermission({}, PERMISSIONS.USER_VIEW)).toBe(false);
  });

  it('hasPermission matches exact claim strings', () => {
    const user = { permissions: [PERMISSIONS.USER_VIEW, PERMISSIONS.REPORT_VIEW] };
    expect(hasPermission(user, PERMISSIONS.USER_VIEW)).toBe(true);
    expect(hasPermission(user, PERMISSIONS.USER_MANAGE)).toBe(false);
  });

  it('hasAnyPermission requires at least one match and a non-empty required list', () => {
    const user = { permissions: [PERMISSIONS.BACKUP_MANAGE] };
    expect(hasAnyPermission(user, [PERMISSIONS.SETTINGS_MANAGE, PERMISSIONS.BACKUP_MANAGE])).toBe(
      true
    );
    expect(hasAnyPermission(user, [PERMISSIONS.SETTINGS_MANAGE])).toBe(false);
    expect(hasAnyPermission(user, [])).toBe(false);
    expect(hasAnyPermission(null, [PERMISSIONS.BACKUP_MANAGE])).toBe(false);
  });

  it('hasAllPermissions requires every listed claim', () => {
    const user = {
      permissions: [PERMISSIONS.REPORT_EXPORT, PERMISSIONS.AUDIT_VIEW],
    };
    expect(hasAllPermissions(user, [PERMISSIONS.REPORT_EXPORT, PERMISSIONS.AUDIT_VIEW])).toBe(true);
    expect(hasAllPermissions(user, [PERMISSIONS.REPORT_EXPORT, PERMISSIONS.SYSTEM_CRITICAL])).toBe(
      false
    );
    expect(hasAllPermissions(user, [])).toBe(false);
    expect(hasAllPermissions({ permissions: [] }, [PERMISSIONS.AUDIT_VIEW])).toBe(false);
  });
});
