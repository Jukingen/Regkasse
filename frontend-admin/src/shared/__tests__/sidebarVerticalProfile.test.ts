import type { MenuProps } from 'antd';
import { describe, expect, it } from 'vitest';

import { buildAdminSidebarMenuItems } from '@/shared/buildAdminSidebar';
import {
  type VerticalProfileMenuScope,
  isCatalogItemVisibleForVerticalProfile,
  resolveVerticalProfileRouteAccess,
} from '@/shared/sidebarVerticalProfile';

const GATED = {
  rooms: '/admin/rooms',
  tickets: '/admin/tickets/redemptions',
  modifierGroups: '/modifier-groups',
  tables: '/tables',
  kitchen: '/admin/kitchen',
} as const;

const UNIVERSAL = ['/dashboard', '/admin/users', '/backup', '/settings'] as const;

function leafKeys(scope: VerticalProfileMenuScope): string[] {
  const { menuItems } = buildAdminSidebarMenuItems({
    t: (key) => key,
    verificationNavLabel: 'verify',
    verticalProfile: scope,
  });
  const keys: string[] = [];
  const walk = (items: MenuProps['items'] | undefined) => {
    for (const item of items ?? []) {
      if (!item || typeof item !== 'object' || ('type' in item && item.type === 'divider'))
        continue;
      const node = item as { key?: string; children?: MenuProps['items'] };
      if (node.children?.length) {
        walk(node.children);
        continue;
      }
      if (typeof node.key === 'string') keys.push(node.key);
    }
  };
  walk(menuItems);
  return keys;
}

const profiles: Array<{ id: string; posFeatures: Record<string, boolean> }> = [
  { id: 'gastronomy', posFeatures: { kitchenDisplay: true, tables: false } },
  { id: 'gastronomy-tables', posFeatures: { kitchenDisplay: true, tables: true } },
  { id: 'vet', posFeatures: { patientRecord: true } },
  { id: 'taxi', posFeatures: { routeTracking: true } },
  { id: 'hair-salon', posFeatures: { appointment: true } },
  { id: 'handy-shop', posFeatures: { imeiTracking: true } },
  { id: 'mobile-services', posFeatures: { routeTracking: true, appointment: true } },
  { id: 'ticket-sales', posFeatures: { ticketScan: true } },
  { id: 'beherbergung', posFeatures: { roomTracking: true, kitchenDisplay: true, tables: false } },
];

describe('sidebar vertical profile filter', () => {
  it.each(profiles)('keeps only matching leaves for $id', ({ id, posFeatures }) => {
    const keys = leafKeys({ profileId: id, posFeatures, isLoading: false });

    for (const universal of UNIVERSAL) {
      expect(keys, universal).toContain(universal);
    }

    expect(keys.includes(GATED.rooms)).toBe(id === 'beherbergung');
    expect(keys.includes('/admin/patients')).toBe(id === 'vet');
    expect(keys.includes('/admin/appointments')).toBe(id === 'hair-salon');
    expect(keys.includes('/admin/imeis')).toBe(id === 'handy-shop');
    expect(keys.includes('/admin/taxi-trips')).toBe(id === 'taxi');
    expect(keys.includes('/admin/customer-addresses')).toBe(id === 'mobile-services');
    expect(keys.includes(GATED.tickets)).toBe(id === 'ticket-sales');
    expect(keys.includes(GATED.modifierGroups)).toBe(
      id === 'gastronomy' || id === 'gastronomy-tables'
    );
    expect(keys.includes(GATED.tables)).toBe(posFeatures.tables === true);
    expect(keys.includes(GATED.kitchen)).toBe(posFeatures.kitchenDisplay === true);
  });

  it('hides gated leaves while the profile is loading and keeps universal leaves', () => {
    const keys = leafKeys({
      profileId: '',
      posFeatures: {},
      isLoading: true,
    });
    expect(keys).toEqual(expect.arrayContaining([...UNIVERSAL]));
    for (const gated of [
      ...Object.values(GATED),
      '/admin/patients',
      '/admin/appointments',
      '/admin/imeis',
      '/admin/taxi-trips',
      '/admin/customer-addresses',
    ]) {
      expect(keys).not.toContain(gated);
    }
  });

  it('hides the patient menu when a vet tenant is shown as gastronomy', () => {
    const keys = leafKeys({
      profileId: 'gastronomy',
      posFeatures: { kitchenDisplay: true, tables: false },
      isLoading: false,
    });
    expect(keys).not.toContain('/admin/patients');
  });

  it('hides a leaf when the profile is listed in hiddenForProfiles', () => {
    const item = {
      hiddenForProfiles: ['gastronomy'],
      requiredFeature: undefined,
      onlyForProfiles: undefined,
    };
    expect(
      isCatalogItemVisibleForVerticalProfile(item, {
        profileId: 'gastronomy',
        posFeatures: {},
      })
    ).toBe(false);
    expect(
      isCatalogItemVisibleForVerticalProfile(item, {
        profileId: 'vet',
        posFeatures: {},
      })
    ).toBe(true);
    expect(
      resolveVerticalProfileRouteAccess('/admin/rooms', {
        profileId: 'vet',
        posFeatures: { roomTracking: true },
      })
    ).toBe('deny');
  });
});
