import {
  SIDEBAR_NAV_ITEM_CATALOG,
  type SidebarNavCatalogItem,
} from '@/shared/adminSidebarRegistry';

/** Ambient profile snapshot used to show or hide sidebar leaves and deep links. */
export type VerticalProfileMenuScope = {
  profileId: string;
  posFeatures: Record<string, boolean>;
  isLoading?: boolean;
  /** Super Admin "all profiles" — skip feature and profile gates. */
  viewAllProfiles?: boolean;
};

export function hasVerticalProfileGate(
  item: Pick<SidebarNavCatalogItem, 'requiredFeature' | 'onlyForProfiles' | 'hiddenForProfiles'>
): boolean {
  return Boolean(
    item.requiredFeature || item.onlyForProfiles?.length || item.hiddenForProfiles?.length
  );
}

/**
 * Feature and profile gates. Ungated leaves stay visible.
 * While the profile is loading, gated leaves stay hidden.
 */
export function isCatalogItemVisibleForVerticalProfile(
  item: Pick<SidebarNavCatalogItem, 'requiredFeature' | 'onlyForProfiles' | 'hiddenForProfiles'>,
  scope: VerticalProfileMenuScope
): boolean {
  if (scope.viewAllProfiles) {
    return true;
  }
  if (!hasVerticalProfileGate(item)) {
    return true;
  }
  if (scope.isLoading) {
    return false;
  }
  if (item.onlyForProfiles?.length && !item.onlyForProfiles.includes(scope.profileId)) {
    return false;
  }
  if (item.hiddenForProfiles?.length && item.hiddenForProfiles.includes(scope.profileId)) {
    return false;
  }
  if (item.requiredFeature && scope.posFeatures[item.requiredFeature] !== true) {
    return false;
  }
  return true;
}

function normalizeAdminPath(pathname: string): string {
  const path = pathname.split('?')[0]?.split('#')[0] ?? pathname;
  if (path.length > 1 && path.endsWith('/')) {
    return path.slice(0, -1);
  }
  return path || '/';
}

/** Longest catalog `menuKey` that equals the path or is a parent prefix. */
export function findSidebarCatalogItemForPath(pathname: string): SidebarNavCatalogItem | undefined {
  const path = normalizeAdminPath(pathname);
  let best: SidebarNavCatalogItem | undefined;
  let bestLength = -1;

  for (const item of Object.values(SIDEBAR_NAV_ITEM_CATALOG)) {
    const key = item.menuKey;
    const matches = path === key || path.startsWith(`${key}/`);
    if (matches && key.length > bestLength) {
      best = item;
      bestLength = key.length;
    }
  }

  return best;
}

export function toVerticalProfileMenuScope(value: {
  profileId: string;
  posFeatures: Record<string, boolean>;
  isLoading: boolean;
  viewAllProfiles?: boolean;
}): VerticalProfileMenuScope {
  return {
    profileId: value.profileId,
    posFeatures: value.posFeatures,
    isLoading: value.isLoading,
    viewAllProfiles: value.viewAllProfiles === true,
  };
}

export function resolveVerticalProfileRouteAccess(
  pathname: string,
  scope: VerticalProfileMenuScope
): 'allow' | 'deny' | 'pending' {
  if (scope.viewAllProfiles) {
    return 'allow';
  }
  const item = findSidebarCatalogItemForPath(pathname);
  if (!item || !hasVerticalProfileGate(item)) {
    return 'allow';
  }
  if (scope.isLoading) {
    return 'pending';
  }
  return isCatalogItemVisibleForVerticalProfile(item, scope) ? 'allow' : 'deny';
}
