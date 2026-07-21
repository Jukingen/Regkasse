import { describe, expect, it } from 'vitest';

import { mapMeResponseToAuthUser } from '../mapMeResponseToAuthUser';

describe('mapMeResponseToAuthUser', () => {
  it('maps camelCase tenant fields', () => {
    const u = mapMeResponseToAuthUser({
      id: 'u1',
      tenantId: '9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c',
      tenantDisplayName: 'Default',
      branchId: null,
      branchDisplayName: null,
      permissions: ['a.b'],
    });
    expect(u.tenantId).toBe('9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c');
    expect(u.tenantDisplayName).toBe('Default');
    expect(u.branchId).toBeNull();
    expect(u.branchDisplayName).toBeNull();
    expect(u.permissions).toEqual(['a.b']);
  });

  it('tolerates missing tenant fields (legacy /me)', () => {
    const u = mapMeResponseToAuthUser({
      id: 'u1',
      permissions: [],
    });
    expect(u.tenantId ?? null).toBeNull();
    expect(u.tenantDisplayName ?? null).toBeNull();
  });

  it('uses PascalCase fallbacks', () => {
    const u = mapMeResponseToAuthUser({
      id: undefined,
      Id: 'u2',
      tenantId: undefined,
      TenantId: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
      tenantDisplayName: undefined,
      TenantDisplayName: 'Org',
      Permissions: ['x'],
    });
    expect(u.id).toBe('u2');
    expect(u.tenantId).toBe('aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee');
    expect(u.tenantDisplayName).toBe('Org');
  });
});
