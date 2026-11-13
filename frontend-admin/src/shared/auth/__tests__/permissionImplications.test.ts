import { describe, expect, it } from 'vitest';

import {
  PERMISSION_IMPLICATIONS,
  findImplicationSources,
  getImpliedPermissions,
  isPermissionImpliedOnly,
  permissionImplied,
} from '../permissionImplications';

describe('permissionImplications', () => {
  it('exports canonical manage→view implications (no inventedish aliases)', () => {
    expect(getImpliedPermissions('user.manage')).toContain('user.view');
    expect(getImpliedPermissions('cash_register.manage')).toContain('cash_register.view');
    expect(getImpliedPermissions('product.manage')).toContain('product.view');
    expect(getImpliedPermissions('digital.manage')).toEqual(
      expect.arrayContaining(['digital.view', 'digital.orders.view', 'digital.orders.manage'])
    );
    expect(getImpliedPermissions('report.export')).toContain('report.view');
    expect(getImpliedPermissions('settings.manage')).toEqual(
      expect.arrayContaining(['settings.view', 'backup.manage', 'website.manage'])
    );
    expect(PERMISSION_IMPLICATIONS['users.manage']).toBeUndefined();
    expect(PERMISSION_IMPLICATIONS['cashregister.manage']).toBeUndefined();
    expect(PERMISSION_IMPLICATIONS['backup.view']).toBeUndefined();
  });

  it('findImplicationSources lists holders that cover a required permission', () => {
    expect(findImplicationSources('user.view', ['user.manage'])).toEqual(['user.manage']);
    expect(findImplicationSources('user.view', ['user.view'])).toEqual([]);
    expect(findImplicationSources('digital.orders.view', ['digital.manage'])).toContain(
      'digital.manage'
    );
  });

  it('isPermissionImpliedOnly is true only when not directly held', () => {
    expect(isPermissionImpliedOnly('user.view', ['user.manage'])).toBe(true);
    expect(isPermissionImpliedOnly('user.view', ['user.view', 'user.manage'])).toBe(false);
    expect(permissionImplied('user.view', ['user.manage'])).toBe(true);
  });
});
