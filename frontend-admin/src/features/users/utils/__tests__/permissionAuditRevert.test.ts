import { describe, expect, it } from 'vitest';

import {
  parseRolePermissionsPayload,
  resolvePermissionAuditRevert,
} from '../permissionAuditRevert';

describe('permissionAuditRevert', () => {
  it('parses role permission payloads', () => {
    const parsed = parseRolePermissionsPayload(
      JSON.stringify({ roleName: 'Custom', permissions: ['sale.view', 'user.view'] })
    );
    expect(parsed?.roleName).toBe('Custom');
    expect(parsed?.permissions).toEqual(['sale.view', 'user.view']);
  });

  it('reverts ROLE_PERMISSIONS_UPDATE to old permissions', () => {
    const revert = resolvePermissionAuditRevert(
      {
        action: 'ROLE_PERMISSIONS_UPDATE',
        oldValues: JSON.stringify({
          roleName: 'Custom',
          permissions: ['sale.view'],
        }),
      },
      { roleName: 'Custom' }
    );
    expect(revert).toEqual({
      kind: 'rolePermissions',
      roleName: 'Custom',
      permissions: ['sale.view'],
    });
  });

  it('reverts override deletion by upserting old values', () => {
    const revert = resolvePermissionAuditRevert(
      {
        action: 'USER_PERMISSION_OVERRIDES_CHANGED',
        oldValues: JSON.stringify({
          permission: 'report.view',
          isGranted: true,
          id: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
        }),
        newValues: JSON.stringify({ removed: true, overrideId: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee' }),
        entityName: 'user-1',
      },
      { userId: 'user-1' }
    );
    expect(revert.kind).toBe('overrideUpsert');
    if (revert.kind === 'overrideUpsert') {
      expect(revert.permission).toBe('report.view');
      expect(revert.isGranted).toBe(true);
    }
  });

  it('marks ROLE_CREATE as unsupported for automatic revert', () => {
    const revert = resolvePermissionAuditRevert(
      { action: 'ROLE_CREATE', newValues: JSON.stringify({ roleName: 'X', permissions: [] }) },
      { roleName: 'X' }
    );
    expect(revert.kind).toBe('unsupported');
  });
});
