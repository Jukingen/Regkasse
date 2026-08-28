/**
 * Resolve a stored favorite menu key to label, href, and icon for the favorites strip.
 */

import {
  ADMIN_SIDEBAR_GROUP_KEYS,
  ADMIN_SIDEBAR_GROUP_ROUTES,
  RKSV_HUB_PATH,
} from '@/shared/adminSidebarNavigation';
import {
  SIDEBAR_GROUP_META,
  SIDEBAR_LAYOUT_ROWS,
  SIDEBAR_NAV_ITEM_CATALOG,
  type SidebarIconToken,
  type SidebarLayoutBlock,
  type SidebarLayoutRow,
} from '@/shared/adminSidebarRegistry';
import { FISCAL_RKSV_CLOSING_SIDEBAR_LEAVES } from '@/shared/fiscalRksvClosingSidebar';

export type SidebarFavoriteDescriptor = {
  menuKey: string;
  href: string;
  labelKey: string;
  icon?: SidebarIconToken;
};

type GroupIndexEntry = { labelKey: string; icon?: SidebarIconToken };

function indexNestedBlock(
  map: Map<string, GroupIndexEntry>,
  block: SidebarLayoutBlock | Extract<SidebarLayoutRow, { kind: 'nested' }>
): void {
  if (block.kind === 'nested' || block.kind === 'fiscalRksvClosing' || block.kind === 'rksvHub') {
    map.set(block.menuKey, { labelKey: block.labelKey, icon: block.icon });
  }
  if (block.kind === 'nested') {
    for (const child of block.childGroups ?? []) {
      map.set(child.menuKey, { labelKey: child.labelKey, icon: child.icon });
    }
  }
}

function buildGroupIndex(): Map<string, GroupIndexEntry> {
  const map = new Map<string, GroupIndexEntry>();
  for (const meta of Object.values(SIDEBAR_GROUP_META)) {
    map.set(meta.menuKey, { labelKey: meta.labelKey, icon: meta.icon });
  }
  for (const row of SIDEBAR_LAYOUT_ROWS) {
    if (row.kind === 'nested') {
      indexNestedBlock(map, row);
      continue;
    }
    if (row.kind !== 'group') continue;
    for (const block of row.blocks) {
      indexNestedBlock(map, block);
    }
  }
  return map;
}

const GROUP_INDEX = buildGroupIndex();

const CATALOG_BY_MENU_KEY = new Map(
  Object.values(SIDEBAR_NAV_ITEM_CATALOG).map((item) => [item.menuKey, item])
);

const FISCAL_BY_MENU_KEY = new Map(
  FISCAL_RKSV_CLOSING_SIDEBAR_LEAVES.map((item) => [item.menuKey, item])
);

function firstAllowedRoute(
  routes: readonly string[] | undefined,
  visibleMenuKeys: ReadonlySet<string>
): string | undefined {
  if (!routes?.length) return undefined;
  return routes.find((route) => visibleMenuKeys.has(route)) ?? routes[0];
}

export function resolveSidebarFavorite(
  menuKey: string,
  visibleMenuKeys: ReadonlySet<string>
): SidebarFavoriteDescriptor | null {
  const catalog = CATALOG_BY_MENU_KEY.get(menuKey);
  if (catalog) {
    return {
      menuKey,
      href: catalog.href,
      labelKey: catalog.labelKey,
      icon: catalog.icon,
    };
  }

  const fiscal = FISCAL_BY_MENU_KEY.get(menuKey);
  if (fiscal) {
    return {
      menuKey,
      href: fiscal.href,
      labelKey: fiscal.labelKey,
    };
  }

  const group = GROUP_INDEX.get(menuKey);
  if (group) {
    const routes = ADMIN_SIDEBAR_GROUP_ROUTES[menuKey];
    const href =
      menuKey === ADMIN_SIDEBAR_GROUP_KEYS.rksv
        ? RKSV_HUB_PATH
        : (firstAllowedRoute(routes, visibleMenuKeys) ?? routes?.[0] ?? '/dashboard');
    return {
      menuKey,
      href,
      labelKey: group.labelKey,
      icon: group.icon,
    };
  }

  if (menuKey.startsWith('/')) {
    return {
      menuKey,
      href: menuKey,
      labelKey: menuKey,
    };
  }

  return null;
}
