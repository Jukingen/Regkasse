import { beforeEach, describe, expect, it } from 'vitest';

import {
  createSavedPermissionAuditFilterId,
  decodePermissionAuditFilterShare,
  deletePermissionAuditFilter,
  encodePermissionAuditFilterShare,
  loadPersonalPermissionAuditFilters,
  loadSharedPermissionAuditFilters,
  savePermissionAuditFilter,
  type SavedPermissionAuditFilter,
} from '@/features/users/utils/permissionAuditSavedFilters';

function sampleFilter(overrides?: Partial<SavedPermissionAuditFilter>): SavedPermissionAuditFilter {
  return {
    id: 'f1',
    name: 'Mine',
    shared: false,
    createdAt: '2026-08-01T00:00:00Z',
    filters: {
      quickFilters: ['role'],
      search: '',
    } as never,
    ...overrides,
  };
}

describe('permissionAuditSavedFilters', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('returns empty lists for missing/invalid storage', () => {
    expect(loadPersonalPermissionAuditFilters('')).toEqual([]);
    expect(loadPersonalPermissionAuditFilters('u1')).toEqual([]);
    window.localStorage.setItem('fa_permission_audit_filters_personal_v1:u1', '{bad');
    expect(loadPersonalPermissionAuditFilters('u1')).toEqual([]);
    window.localStorage.setItem('fa_permission_audit_filters_personal_v1:u1', JSON.stringify({ a: 1 }));
    expect(loadPersonalPermissionAuditFilters('u1')).toEqual([]);
  });

  it('saves/loads/deletes personal filters', () => {
    const saved = savePermissionAuditFilter(sampleFilter(), { userId: 'u1', tenantId: 't1' });
    expect(saved).toHaveLength(1);
    expect(loadPersonalPermissionAuditFilters('u1')[0]?.name).toBe('Mine');

    savePermissionAuditFilter(sampleFilter({ name: 'Updated' }), {
      userId: 'u1',
      tenantId: 't1',
    });
    expect(loadPersonalPermissionAuditFilters('u1')[0]?.name).toBe('Updated');

    deletePermissionAuditFilter('f1', { userId: 'u1', tenantId: 't1', shared: false });
    expect(loadPersonalPermissionAuditFilters('u1')).toEqual([]);
  });

  it('saves/loads/deletes shared filters under tenant key', () => {
    savePermissionAuditFilter(sampleFilter({ shared: true, id: 's1' }), {
      userId: 'u1',
      tenantId: 'tenant-a',
    });
    expect(loadSharedPermissionAuditFilters('tenant-a')).toHaveLength(1);
    deletePermissionAuditFilter('s1', { userId: 'u1', tenantId: 'tenant-a', shared: true });
    expect(loadSharedPermissionAuditFilters('tenant-a')).toEqual([]);
  });

  it('round-trips share tokens and rejects invalid ones', () => {
    const filter = sampleFilter({ id: 'share-1', name: 'Shared preset' });
    const token = encodePermissionAuditFilterShare(filter);
    expect(decodePermissionAuditFilterShare(token)?.id).toBe('share-1');
    expect(decodePermissionAuditFilterShare('%%%')).toBeNull();
    expect(decodePermissionAuditFilterShare(btoa(JSON.stringify({ id: 1 })))).toBeNull();
  });

  it('creates filter ids', () => {
    expect(createSavedPermissionAuditFilterId().length).toBeGreaterThan(8);
  });
});
