import { describe, expect, it } from 'vitest';
import { getStaffPolicy } from '@/shared/auth/staffPolicy';
import { PERMISSIONS } from '@/shared/auth/permissions';

describe('getStaffPolicy', () => {
  it('grants Manager read-only staff access without user.manage', () => {
    const policy = getStaffPolicy([
      PERMISSIONS.USER_VIEW,
      PERMISSIONS.REPORT_VIEW,
      PERMISSIONS.SHIFT_VIEW,
    ]);

    expect(policy.canView).toBe(true);
    expect(policy.canViewActivity).toBe(true);
    expect(policy.canViewActivityReport).toBe(true);
    expect(policy.canViewTenantMemberships).toBe(true);
    expect(policy.canManage).toBe(false);
  });

  it('denies staff access without user.view', () => {
    const policy = getStaffPolicy([PERMISSIONS.REPORT_VIEW]);

    expect(policy.canView).toBe(false);
    expect(policy.canViewActivity).toBe(false);
    expect(policy.canViewTenantMemberships).toBe(false);
    expect(policy.canViewActivityReport).toBe(true);
  });
});
