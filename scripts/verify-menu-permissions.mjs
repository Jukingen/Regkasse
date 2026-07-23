#!/usr/bin/env node
/**
 * Menu → permission mapping audit (FA sidebar / IA registry).
 *
 * Ensures every visible (and deep-link catalog) menu leaf has a `ROUTE_PERMISSIONS`
 * entry so sidebar filtering (`isMenuItemAllowed` / `MENU_PERMISSION`) stays consistent.
 *
 * Also checks `menuPermissionRegistry` primary paths and permission literals.
 *
 * Sources:
 *   - frontend-admin/src/shared/adminSidebarRegistry.ts (SIDEBAR_NAV_ITEM_CATALOG menuKey)
 *   - frontend-admin/src/features/rksv/rksvAdminMenuModel.ts (RKSV leaf keys)
 *   - frontend-admin/src/shared/fiscalRksvClosingSidebar.ts (virtual Sonderbeleg keys)
 *   - frontend-admin/src/shared/auth/routePermissions.ts (ROUTE_PERMISSIONS)
 *   - frontend-admin/src/shared/auth/menuPermissionRegistry.ts (IA areas)
 *   - frontend-admin/src/shared/auth/permissions.ts (FA catalog)
 *
 * Run from repository root:
 *   node scripts/verify-menu-permissions.mjs
 *   node scripts/verify-menu-permissions.mjs --list-missing
 *   node scripts/verify-menu-permissions.mjs --include-secondary
 *
 * npm: npm run verify:menu-permissions
 */
import { readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');

const PATHS = {
  faPermissions: join(root, 'frontend-admin/src/shared/auth/permissions.ts'),
  routePermissions: join(root, 'frontend-admin/src/shared/auth/routePermissions.ts'),
  sidebarRegistry: join(root, 'frontend-admin/src/shared/adminSidebarRegistry.ts'),
  rksvMenuModel: join(root, 'frontend-admin/src/features/rksv/rksvAdminMenuModel.ts'),
  fiscalClosing: join(root, 'frontend-admin/src/shared/fiscalRksvClosingSidebar.ts'),
  menuRegistry: join(root, 'frontend-admin/src/shared/auth/menuPermissionRegistry.ts'),
  permissionGroupRegistry: join(
    root,
    'frontend-admin/src/shared/auth/permissionGroupRegistry.ts'
  ),
  backupAreaRoutes: join(root, 'frontend-admin/src/shared/backupAreaRoutes.ts'),
  settingsAreaRoutes: join(root, 'frontend-admin/src/shared/settingsAreaRoutes.ts'),
};

const args = new Set(process.argv.slice(2));
const listMissing = args.has('--list-missing');
const includeSecondary = args.has('--include-secondary');
const help = args.has('--help') || args.has('-h');

if (help) {
  console.log(`Usage: node scripts/verify-menu-permissions.mjs [options]

Checks every FA menu leaf has a ROUTE_PERMISSIONS mapping (menu visibility gate).

  --list-missing         Print each missing menu key on its own line
  --include-secondary    Also require Backup / Settings secondary-nav paths
`);
  process.exit(0);
}

function fail(msg) {
  console.error(`✗ ${msg}`);
  process.exitCode = 1;
}

function ok(msg) {
  console.log(`✓ ${msg}`);
}

function read(path) {
  if (!existsSync(path)) {
    fail(`Missing file: ${path}`);
    return '';
  }
  return readFileSync(path, 'utf8');
}

function stripTsComments(source) {
  return source
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/(^|[^:])\/\/.*$/gm, '$1');
}

function extractFaCatalog(source) {
  const cleaned = stripTsComments(source);
  const appPermLiterals = new Map();
  const permissionsLiterals = new Map();

  const appBlock = cleaned.match(/export const AppPermissions\s*=\s*\{([\s\S]*?)\}\s*as const/);
  if (appBlock) {
    const re = /(\w+)\s*:\s*'([^']+)'/g;
    let m;
    while ((m = re.exec(appBlock[1]))) {
      appPermLiterals.set(m[1], m[2]);
    }
  }

  const permBlock = cleaned.match(/export const PERMISSIONS\s*=\s*\{([\s\S]*?)\}\s*as const/);
  if (permBlock) {
    const re = /(\w+)\s*:\s*(?:'([^']+)'|AppPermissions\.(\w+))/g;
    let m;
    while ((m = re.exec(permBlock[1]))) {
      if (m[2]) {
        permissionsLiterals.set(m[1], m[2]);
      } else if (m[3]) {
        const resolved = appPermLiterals.get(m[3]);
        if (resolved) permissionsLiterals.set(m[1], resolved);
      }
    }
  }

  return {
    appPermLiterals,
    permissionsLiterals,
    allKeys: new Set([...appPermLiterals.values(), ...permissionsLiterals.values()]),
  };
}

/** Paths registered in ROUTE_PERMISSIONS (exact keys). */
function extractRoutePermissionPaths(source) {
  const cleaned = stripTsComments(source);
  const paths = new Set();
  const lineRe =
    /^\s*(['"])(\/[^'"]*)\1\s*:\s*(\[[^\]]*\]|PERMISSIONS\.\w+|AppPermissions\.\w+|ANY_AUTHENTICATED_PERMISSION)/gm;
  let m;
  while ((m = lineRe.exec(cleaned))) {
    paths.add(m[2]);
  }
  return paths;
}

/** Catalog `menuKey: '/…'` literals (includes sidebarHidden deep-links). */
function extractCatalogMenuKeys(source) {
  const cleaned = stripTsComments(source);
  const keys = new Set();
  const catalogBlock = cleaned.match(
    /export const SIDEBAR_NAV_ITEM_CATALOG[\s\S]*?=\s*\{([\s\S]*?)\n\};/
  );
  const body = catalogBlock?.[1] ?? cleaned;
  const re = /menuKey:\s*'(\/[^']+)'/g;
  let m;
  while ((m = re.exec(body))) {
    keys.add(m[1]);
  }
  return keys;
}

/** RKSV leaf `key: '/rksv/…'` from buildRksvMenuGroups. */
function extractRksvMenuKeys(source) {
  const cleaned = stripTsComments(source);
  const keys = new Set();
  const re = /\bkey:\s*'(\/rksv[^']*)'/g;
  let m;
  while ((m = re.exec(cleaned))) {
    keys.add(m[1]);
  }
  return keys;
}

/** Virtual Sonderbeleg keys from FISCAL_RKSV_CLOSING_VIRTUAL_MENU_KEYS. */
function extractVirtualFiscalKeys(source) {
  const cleaned = stripTsComments(source);
  const keys = new Set();
  const block = cleaned.match(
    /export const FISCAL_RKSV_CLOSING_VIRTUAL_MENU_KEYS\s*=\s*\[([\s\S]*?)\]\s*as const/
  );
  if (!block) return keys;
  const re = /'(\/[^']+)'/g;
  let m;
  while ((m = re.exec(block[1]))) {
    keys.add(m[1]);
  }
  return keys;
}

/** Absolute path string constants / array entries under secondary-nav helpers. */
function extractSecondaryNavPaths(source) {
  const cleaned = stripTsComments(source);
  const keys = new Set();
  const re = /(?:PATH|PATHS)\s*=\s*(?:'(\/[^']+)'|\[([\s\S]*?)\])/g;
  let m;
  while ((m = re.exec(cleaned))) {
    if (m[1]) keys.add(m[1]);
    if (m[2]) {
      const inner = /'(\/[^']+)'/g;
      let im;
      while ((im = inner.exec(m[2]))) keys.add(im[1]);
    }
  }
  // Also harvest standalone `'/…' as const` path exports
  const asConst = /export const \w+_PATH\s*=\s*'(\/[^']+)'/g;
  while ((m = asConst.exec(cleaned))) {
    keys.add(m[1]);
  }
  const areaArrays = /export const \w+_AREA_ROUTE_PATHS\s*=\s*\[([\s\S]*?)\]/g;
  while ((m = areaArrays.exec(cleaned))) {
    const inner = /'(\/[^']+)'/g;
    let im;
    while ((im = inner.exec(m[1]))) keys.add(im[1]);
  }
  return keys;
}

/**
 * menuPermissionRegistry: primary paths + permission string refs.
 */
function extractMenuRegistry(source, fa) {
  const cleaned = stripTsComments(source);
  const primaryPaths = new Map(); // area → path
  const pathBlock = cleaned.match(
    /export const MENU_AREA_PRIMARY_PATH[\s\S]*?=\s*\{([\s\S]*?)\}\s*;/
  );
  if (pathBlock) {
    const re = /(\w+)\s*:\s*'(\/[^']+)'/g;
    let m;
    while ((m = re.exec(pathBlock[1]))) {
      primaryPaths.set(m[1], m[2]);
    }
  }

  const permissionKeys = new Set();
  const unknownRefs = [];
  const menuBlock = cleaned.match(/export const MENU_PERMISSIONS\s*=\s*\{([\s\S]*?)\}\s*as const/);
  if (menuBlock) {
    const refs = menuBlock[1].matchAll(/PERMISSIONS\.(\w+)|AppPermissions\.(\w+)/g);
    for (const r of refs) {
      if (r[1]) {
        const key = fa.permissionsLiterals.get(r[1]);
        if (key) permissionKeys.add(key);
        else unknownRefs.push(`PERMISSIONS.${r[1]}`);
      }
      if (r[2]) {
        const key = fa.appPermLiterals.get(r[2]);
        if (key) permissionKeys.add(key);
        else unknownRefs.push(`AppPermissions.${r[2]}`);
      }
    }
  }

  return { primaryPaths, permissionKeys, unknownRefs: [...new Set(unknownRefs)] };
}

function sorted(setOrArr) {
  return [...setOrArr].sort((a, b) => a.localeCompare(b));
}

// --- main ---
const faSrc = read(PATHS.faPermissions);
const routeSrc = read(PATHS.routePermissions);
const sidebarSrc = read(PATHS.sidebarRegistry);
const rksvSrc = read(PATHS.rksvMenuModel);
const fiscalSrc = read(PATHS.fiscalClosing);
const registrySrc = read(PATHS.menuRegistry);
const permissionGroupSrc = read(PATHS.permissionGroupRegistry);

const fa = extractFaCatalog(faSrc);
const routePaths = extractRoutePermissionPaths(routeSrc);
const catalogKeys = extractCatalogMenuKeys(sidebarSrc);
const rksvKeys = extractRksvMenuKeys(rksvSrc);
const virtualKeys = extractVirtualFiscalKeys(fiscalSrc);
const registry = extractMenuRegistry(registrySrc, fa);

const menuLeaves = new Set([...catalogKeys, ...rksvKeys, ...virtualKeys]);

if (includeSecondary) {
  const backupSrc = read(PATHS.backupAreaRoutes);
  const settingsSrc = read(PATHS.settingsAreaRoutes);
  for (const p of extractSecondaryNavPaths(backupSrc)) menuLeaves.add(p);
  for (const p of extractSecondaryNavPaths(settingsSrc)) menuLeaves.add(p);
}

console.log('\nMenu permission mapping audit');
console.log('─────────────────────────────');
console.log(`SIDEBAR_NAV_ITEM_CATALOG:     ${catalogKeys.size} menuKeys`);
console.log(`RKSV admin menu leaves:       ${rksvKeys.size}`);
console.log(`Fiscal virtual menu keys:     ${virtualKeys.size}`);
console.log(`Unique menu leaves to check:  ${menuLeaves.size}`);
console.log(`ROUTE_PERMISSIONS paths:      ${routePaths.size}`);
console.log(`menuPermissionRegistry areas: ${registry.primaryPaths.size}`);

const missingRoute = sorted([...menuLeaves].filter((k) => !routePaths.has(k)));
if (missingRoute.length) {
  fail(
    `${missingRoute.length} menu leaf(s) missing from ROUTE_PERMISSIONS` +
      (listMissing ? `:\n  - ${missingRoute.join('\n  - ')}` : ' (re-run with --list-missing)')
  );
} else {
  ok('Every menu leaf has an exact ROUTE_PERMISSIONS entry');
}

const registryPathMissing = [];
for (const [area, path] of registry.primaryPaths) {
  if (!routePaths.has(path)) {
    registryPathMissing.push(`${area} → ${path}`);
  }
}
if (registryPathMissing.length) {
  fail(
    `menuPermissionRegistry primary path(s) missing from ROUTE_PERMISSIONS:\n  - ${registryPathMissing.join('\n  - ')}`
  );
} else {
  ok('Every menuPermissionRegistry primary path is in ROUTE_PERMISSIONS');
}

if (registry.unknownRefs.length) {
  fail(
    `menuPermissionRegistry unresolved permission refs:\n  - ${registry.unknownRefs.join('\n  - ')}`
  );
} else {
  ok('menuPermissionRegistry permission refs resolve in FA catalog');
}

const registryKeysNotInFa = sorted(
  [...registry.permissionKeys].filter((k) => !fa.allKeys.has(k))
);
if (registryKeysNotInFa.length) {
  fail(
    `menuPermissionRegistry uses keys not in FA permissions.ts:\n  - ${registryKeysNotInFa.join('\n  - ')}`
  );
} else {
  ok('menuPermissionRegistry permission keys ⊆ FA catalog');
}

// Hub `/rksv` is often selected without being a leaf in rksvAdminMenuModel — require it if catalog/ops references it
if (routePaths.has('/rksv') === false && (catalogKeys.has('/rksv') || rksvKeys.size > 0)) {
  // soft: many leaves live under /rksv/*; hub must still be mapped for isRksvMenuAreaAllowed
  fail('ROUTE_PERMISSIONS must define /rksv (RKSV hub menu area)');
} else if (routePaths.has('/rksv')) {
  ok('RKSV hub /rksv is mapped in ROUTE_PERMISSIONS');
}

// --- permissionGroupRegistry sync ---
const groupSrcClean = stripTsComments(permissionGroupSrc);
const areaToGroupBlock = groupSrcClean.match(
  /export const MENU_AREA_TO_PERMISSION_GROUP[\s\S]*?=\s*\{([\s\S]*?)\}\s*;/
);
const hubAreas = new Set();
const hubBlock = groupSrcClean.match(
  /export const MENU_AREAS_WITHOUT_PERMISSION_GROUP[\s\S]*?=\s*new Set\(\[([\s\S]*?)\]\)/
);
if (hubBlock) {
  const re = /'(\w+)'/g;
  let hm;
  while ((hm = re.exec(hubBlock[1]))) hubAreas.add(hm[1]);
}

const missingAreaGroups = [];
if (areaToGroupBlock) {
  const re = /(\w+)\s*:\s*(null|'([^']+)')/g;
  let m;
  while ((m = re.exec(areaToGroupBlock[1]))) {
    const area = m[1];
    const isNull = m[2] === 'null';
    if (hubAreas.has(area)) {
      if (!isNull) missingAreaGroups.push(`${area} should be null (hub)`);
    } else if (isNull) {
      missingAreaGroups.push(`${area} missing permission group mapping`);
    }
  }
} else {
  fail('MENU_AREA_TO_PERMISSION_GROUP not found in permissionGroupRegistry.ts');
}

if (missingAreaGroups.length) {
  fail(
    `permissionGroupRegistry menu area gaps:\n  - ${missingAreaGroups.join('\n  - ')}`
  );
} else {
  ok('Every menu area has a permission group (hubs excluded)');
}

const inventedish = [];
const permLit = /permissions:\s*\[([\s\S]*?)\]/g;
let pm;
while ((pm = permLit.exec(groupSrcClean))) {
  if (/users\.|cashregister\.|roles\./.test(pm[1])) {
    inventedish.push(pm[1].trim().slice(0, 80));
  }
}
if (inventedish.length) {
  fail(`permissionGroupRegistry uses inventedish keys:\n  - ${inventedish.join('\n  - ')}`);
} else {
  ok('permissionGroupRegistry has no users.*/cashregister.*/roles.* aliases');
}

if (process.exitCode) {
  console.error('\nMenu permission mapping audit FAILED');
  process.exit(1);
}
console.log('\nMenu permission mapping audit OK');
