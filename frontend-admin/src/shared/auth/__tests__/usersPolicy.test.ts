import { describe, expect, it } from 'vitest';

import { PERMISSIONS } from '@/shared/auth/permissions';
import { getUsersPolicy } from '@/shared/auth/usersPolicy';
import { MANAGER_ADMIN_PERMISSIONS } from '@/shared/__tests__/fixtures/adminAppPermissionFixtures';

describe('getUsersPolicy', () => {
  it('allows Manager to view users via user.view permission', () => {
    const policy = getUsersPolicy('Manager', [...MANAGER_ADMIN_PERMISSIONS]);
    expect(policy.canView).toBe(true);
  });

  it('allows Manager to view users via role fallback when JWT omits user.view', () => {
    const withoutUserView = MANAGER_ADMIN_PERMISSIONS.filter((p) => p !== PERMISSIONS.USER_VIEW);
    const policy = getUsersPolicy('Manager', [...withoutUserView]);
    expect(policy.canView).toBe(true);
  });

  it('allows Manager to view users when permissions array is empty (role fallback)', () => {
    expect(getUsersPolicy('Manager', []).canView).toBe(true);
    expect(getUsersPolicy('Manager', undefined).canView).toBe(true);
  });

  it('denies Cashier without user.view', () => {
    const policy = getUsersPolicy('Cashier', [PERMISSIONS.PRODUCT_VIEW, PERMISSIONS.PAYMENT_VIEW]);
    expect(policy.canView).toBe(false);
  });

  it('grants Manager create/edit when user.manage is present', () => {
    const policy = getUsersPolicy('Manager', [...MANAGER_ADMIN_PERMISSIONS]);
    expect(policy.canCreate).toBe(true);
    expect(policy.canEdit).toBe(true);
    expect(policy.canManagePermissions).toBe(true);
    expect(policy.canCreateRole).toBe(false);
  });

  it('does not grant Manager create/edit without user.manage', () => {
    const withoutManage = MANAGER_ADMIN_PERMISSIONS.filter((p) => p !== PERMISSIONS.USER_MANAGE);
    const policy = getUsersPolicy('Manager', [...withoutManage]);
    expect(policy.canCreate).toBe(false);
    expect(policy.canEdit).toBe(false);
    expect(policy.canManagePermissions).toBe(false);
  });
});
