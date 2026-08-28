import { readdirSync } from 'node:fs';
import path from 'node:path';

import { describe, expect, it } from 'vitest';

import {
  canShowPlatformAdminMenu,
  canShowRksvMenu,
  canViewUsers,
  isSuperAdmin,
} from '@/features/auth/constants/roles';
import {
  type SidebarPermissionContext,
  collectSelectableRouteKeysFromMenuItems,
  filterSidebarMenuItems,
} from '@/shared/adminSidebarNavigation';
import {
  SIDEBAR_LAYOUT_ROWS,
  SIDEBAR_NAV_ITEM_CATALOG,
  type SidebarCatalogId,
  type SidebarLayoutBlock,
  type SidebarLayoutRow,
} from '@/shared/adminSidebarRegistry';
import { canAccessPath } from '@/shared/auth/canAccessPath';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import { validateMenuPermissionMappings } from '@/shared/auth/menuPermissionMappingValidation';
import { getRequiredPermissionForPath, ROUTE_PERMISSIONS } from '@/shared/auth/routePermissions';
import { buildAdminSidebarMenuItems } from '@/shared/buildAdminSidebar';

import {
  CASHIER_ADMIN_PERMISSIONS,
  CASHIER_FORBIDDEN_MENU_KEYS,
  CASHIER_REQUIRED_MENU_KEYS,
  MANAGER_ADMIN_PERMISSIONS,
  MANAGER_FORBIDDEN_MENU_KEYS,
  MANAGER_REQUIRED_MENU_KEYS,
} from './fixtures/adminAppPermissionFixtures';
import { PRE_IA_SIDEBAR_LAYOUT_CATALOG_IDS } from './fixtures/preIaSidebarLayoutCatalogIds';

const passthroughT = (key: string) => key;

/** Catalog ids that are visible in the catalog but were never wired into SIDEBAR_LAYOUT_ROWS (pre-IA). */
const DOCUMENTED_LAYOUT_OMISSIONS = new Set<string>(['licenseDebug']);

function collectLayoutCatalogIds(rows: readonly SidebarLayoutRow[]): string[] {
  const ids: string[] = [];

  const walkBlock = (block: SidebarLayoutBlock) => {
    if (block.kind === 'leaves' || block.kind === 'nested') {
      ids.push(...block.catalogIds);
      if (block.kind === 'nested') {
        for (const child of block.childGroups ?? []) {
          ids.push(...child.catalogIds);
        }
      }
    }
  };

  for (const row of rows) {
    if (row.kind === 'leaves' || row.kind === 'nested') {
      ids.push(...row.catalogIds);
      continue;
    }
    if (row.kind !== 'group') continue;
    for (const block of row.blocks) walkBlock(block);
  }

  return ids;
}

function hrefPathname(href: string): string {
  const withoutHash = href.split('#')[0] ?? href;
  const pathOnly = withoutHash.split('?')[0] ?? withoutHash;
  return pathOnly.replace(/\/+$/, '') || '/';
}

function collectAppPageRoutes(appDir: string): Set<string> {
  const routes = new Set<string>();

  const walk = (dir: string, urlParts: string[]) => {
    for (const ent of readdirSync(dir, { withFileTypes: true })) {
      if (ent.name.startsWith('_')) continue;
      const full = path.join(dir, ent.name);
      if (ent.isDirectory()) {
        const isGroup = /^\(.*\)$/.test(ent.name);
        walk(full, isGroup ? urlParts : [...urlParts, ent.name]);
        continue;
      }
      if (ent.name === 'page.tsx' || ent.name === 'page.ts') {
        routes.add(`/${urlParts.join('/')}`);
      }
    }
  };

  walk(appDir, []);
  return routes;
}

function pageExistsForPathname(pathname: string, pages: ReadonlySet<string>): boolean {
  if (pages.has(pathname)) return true;
  return [...pages].some((route) => {
    const routeParts = route.split('/').filter(Boolean);
    const pathParts = pathname.split('/').filter(Boolean);
    if (routeParts.length !== pathParts.length) return false;
    return routeParts.every(
      (seg, idx) => (seg.startsWith('[') && seg.endsWith(']')) || seg === pathParts[idx]
    );
  });
}

function buildSidebarCtx(role: string, permissions: readonly string[]): SidebarPermissionContext {
  return {
    usePermissionFirst: true,
    permissions: [...permissions],
    userRole: role,
    isMenuItemAllowed,
    canViewUsers,
    canShowRksvMenu,
    canShowPlatformAdminMenu,
    isSuperAdminRole: isSuperAdmin,
  };
}

describe('sidebar IA no-loss / route / permission contract', () => {
  const layoutIds = collectLayoutCatalogIds(SIDEBAR_LAYOUT_ROWS);

  it('keeps every pre-IA layout catalog id (no menu leaf dropped)', () => {
    const current = new Set(layoutIds);
    const missing = PRE_IA_SIDEBAR_LAYOUT_CATALOG_IDS.filter((id) => !current.has(id));
    expect(missing, `Dropped layout catalog ids: ${missing.join(', ')}`).toEqual([]);
  });

  it('does not remove catalog entries', () => {
    for (const id of PRE_IA_SIDEBAR_LAYOUT_CATALOG_IDS) {
      expect(SIDEBAR_NAV_ITEM_CATALOG[id as SidebarCatalogId], id).toBeDefined();
    }
  });

  it('wires every visible non-omitted catalog leaf into the layout', () => {
    const layout = new Set(layoutIds);
    const unwired: string[] = [];
    for (const [id, item] of Object.entries(SIDEBAR_NAV_ITEM_CATALOG)) {
      if (item.sidebarHidden) continue;
      if (DOCUMENTED_LAYOUT_OMISSIONS.has(id)) continue;
      if (!layout.has(id)) unwired.push(id);
    }
    expect(unwired, `Visible catalog ids missing from layout: ${unwired.join(', ')}`).toEqual([]);
  });

  it('emits every non-hidden layout leaf in the built Ant Design menu', () => {
    const { menuItems } = buildAdminSidebarMenuItems({
      t: passthroughT,
      verificationNavLabel: 'Verifications',
    });
    const built = new Set(collectSelectableRouteKeysFromMenuItems(menuItems));
    const missing: string[] = [];
    for (const id of layoutIds) {
      const item = SIDEBAR_NAV_ITEM_CATALOG[id as SidebarCatalogId];
      if (!item || item.sidebarHidden) continue;
      if (item.developmentOnly && process.env.NODE_ENV !== 'development') continue;
      if (!built.has(item.menuKey)) missing.push(`${id} (${item.menuKey})`);
    }
    expect(missing, `Built menu missing layout leaves: ${missing.join(', ')}`).toEqual([]);
  });

  it('keeps catalog.permission aligned with ROUTE_PERMISSIONS and MENU_PERMISSION coverage', () => {
    const issues = validateMenuPermissionMappings(SIDEBAR_NAV_ITEM_CATALOG, ROUTE_PERMISSIONS);
    expect(issues, JSON.stringify(issues, null, 2)).toEqual([]);

    for (const item of Object.values(SIDEBAR_NAV_ITEM_CATALOG)) {
      expect(
        getRequiredPermissionForPath(item.menuKey),
        `ROUTE_PERMISSIONS must cover menuKey ${item.menuKey}`
      ).toBeDefined();
      expect(
        getRequiredPermissionForPath(hrefPathname(item.href)),
        `ROUTE_PERMISSIONS must cover href ${item.href}`
      ).toBeDefined();
    }
  });

  it('resolves every catalog href to an App Router page', () => {
    const appDir = path.join(process.cwd(), 'src', 'app');
    const pages = collectAppPageRoutes(appDir);
    const missing: string[] = [];

    for (const item of Object.values(SIDEBAR_NAV_ITEM_CATALOG)) {
      const pathname = hrefPathname(item.href);
      if (!pageExistsForPathname(pathname, pages)) {
        missing.push(`${item.id} → ${pathname} (menuKey ${item.menuKey})`);
      }
    }

    expect(missing, `No page.tsx for catalog hrefs:\n${missing.join('\n')}`).toEqual([]);
  });

  it('keeps Cashier / Manager / SuperAdmin visibility contracts', () => {
    const cashierKeys = collectSelectableRouteKeysFromMenuItems(
      filterSidebarMenuItems(
        buildAdminSidebarMenuItems({
          t: passthroughT,
          verificationNavLabel: 'Verifications',
        }).menuItems,
        buildSidebarCtx('Cashier', CASHIER_ADMIN_PERMISSIONS)
      )
    );
    for (const key of CASHIER_REQUIRED_MENU_KEYS) {
      expect(cashierKeys, `Cashier must see ${key}`).toContain(key);
    }
    for (const key of CASHIER_FORBIDDEN_MENU_KEYS) {
      expect(cashierKeys, `Cashier must not see ${key}`).not.toContain(key);
    }

    const managerKeys = collectSelectableRouteKeysFromMenuItems(
      filterSidebarMenuItems(
        buildAdminSidebarMenuItems({
          t: passthroughT,
          verificationNavLabel: 'Verifications',
        }).menuItems,
        buildSidebarCtx('Manager', MANAGER_ADMIN_PERMISSIONS)
      )
    );
    const managerSet = new Set(managerKeys);
    const missingRequired = MANAGER_REQUIRED_MENU_KEYS.filter((key) => !managerSet.has(key));
    expect(
      missingRequired,
      `Manager lost required menus: ${missingRequired.join(', ')}`
    ).toEqual([]);
    for (const key of MANAGER_FORBIDDEN_MENU_KEYS) {
      expect(managerKeys, `Manager must not see ${key}`).not.toContain(key);
      expect(canAccessPath(key, [...MANAGER_ADMIN_PERMISSIONS]), key).toBe(false);
    }

    const superKeys = collectSelectableRouteKeysFromMenuItems(
      filterSidebarMenuItems(
        buildAdminSidebarMenuItems({
          t: passthroughT,
          verificationNavLabel: 'Verifications',
        }).menuItems,
        buildSidebarCtx('SuperAdmin', [])
      )
    );
    expect(superKeys).toContain('/admin/tenants');
    expect(superKeys).toContain('/dashboard');
    expect(superKeys.length).toBeGreaterThan(managerKeys.length);
  });
});
