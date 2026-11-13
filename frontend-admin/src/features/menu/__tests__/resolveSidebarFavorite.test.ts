import { describe, expect, it } from 'vitest';

import { resolveSidebarFavorite } from '@/features/menu/resolveSidebarFavorite';
import { ADMIN_SIDEBAR_GROUP_KEYS, RKSV_HUB_PATH } from '@/shared/adminSidebarNavigation';

describe('resolveSidebarFavorite', () => {
  const visible = new Set([
    '/receipts',
    '/products',
    '/dashboard',
    '/operations-center',
    ADMIN_SIDEBAR_GROUP_KEYS.operations,
    ADMIN_SIDEBAR_GROUP_KEYS.rksv,
    ADMIN_SIDEBAR_GROUP_KEYS.dashboard,
  ]);

  it('resolves catalog leaves', () => {
    const receipts = resolveSidebarFavorite('/receipts', visible);
    expect(receipts?.href).toBe('/receipts');
    expect(receipts?.labelKey).toBe('nav.receipts');
    expect(receipts?.icon).toBe('FileSearchOutlined');
  });

  it('resolves Verkauf group to a visible operations route', () => {
    const verkauf = resolveSidebarFavorite(ADMIN_SIDEBAR_GROUP_KEYS.operations, visible);
    expect(verkauf?.labelKey).toBe('nav.verkauf');
    expect(verkauf?.href).toBe('/operations-center');
  });

  it('resolves RKSV group to the RKSV hub path', () => {
    const rksv = resolveSidebarFavorite(ADMIN_SIDEBAR_GROUP_KEYS.rksv, visible);
    expect(rksv?.labelKey).toBe('nav.rksv.title');
    expect(rksv?.href).toBe(RKSV_HUB_PATH);
  });

  it('returns null for unknown non-path keys', () => {
    expect(resolveSidebarFavorite('grp-does-not-exist', visible)).toBeNull();
  });
});
