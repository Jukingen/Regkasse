#!/usr/bin/env node
/**
 * Permission key consistency audit (backend ↔ FA catalog ↔ routes ↔ sidebar).
 *
 * Sources of truth (string values must match exactly — case-sensitive):
 *   1. backend/Authorization/AppPermissions.cs
 *   2. frontend-admin/src/shared/auth/permissions.ts  (+ permissionsCatalog.ts re-export)
 *   3. frontend-admin/src/shared/auth/routePermissions.ts  (ROUTE_PERMISSIONS values)
 *   4. frontend-admin/src/shared/adminSidebarRegistry.ts (catalog `permission` fields)
 *
 * Run from repository root:
 *   node scripts/verify-permission-keys.mjs
 *   node scripts/verify-permission-keys.mjs --table
 *   node scripts/verify-permission-keys.mjs --strict-fa-complete   # fail if FA omits any backend key
 *
 * npm: npm run verify:permission-keys
 */
import { readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');

const PATHS = {
  backend: join(root, 'backend/Authorization/AppPermissions.cs'),
  faPermissions: join(root, 'frontend-admin/src/shared/auth/permissions.ts'),
  faCatalog: join(root, 'frontend-admin/src/shared/auth/permissionsCatalog.ts'),
  routePermissions: join(root, 'frontend-admin/src/shared/auth/routePermissions.ts'),
  sidebarRegistry: join(root, 'frontend-admin/src/shared/adminSidebarRegistry.ts'),
};

const args = new Set(process.argv.slice(2));
const wantTable = args.has('--table');
const strictFaComplete = args.has('--strict-fa-complete');
const help = args.has('--help') || args.has('-h');

if (help) {
  console.log(`Usage: node scripts/verify-permission-keys.mjs [--table] [--strict-fa-complete]

Checks permission string keys across:
  AppPermissions.cs, permissions.ts / permissionsCatalog.ts,
  routePermissions.ts, adminSidebarRegistry.ts

  --table               Print Backend | FA Catalog | Route | Menu mapping
  --strict-fa-complete  Fail when FA typed catalog omits a backend key
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

/** Extract `public const string X = "key";` from AppPermissions.cs */
function extractBackendKeys(source) {
  const keys = new Map(); // key → const name
  const re = /public const string (\w+)\s*=\s*"([^"]+)";/g;
  let m;
  while ((m = re.exec(source))) {
    keys.set(m[2], m[1]);
  }
  return keys;
}

/** Strip line/block comments so JSDoc like `Backend: AppPermissions.X` is not parsed as code. */
function stripTsComments(source) {
  return source
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/(^|[^:])\/\/.*$/gm, '$1');
}

/**
 * Extract FA AppPermissions + PERMISSIONS string values and const → key maps.
 * Handles both `'x.y'` literals and `AppPermissions.Foo` references.
 */
function extractFaCatalog(source) {
  const cleaned = stripTsComments(source);
  const appPermLiterals = new Map(); // PascalCase → key
  const permissionsLiterals = new Map(); // SCREAMING → key or ref

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
        else fail(`PERMISSIONS.${m[1]} references unknown AppPermissions.${m[3]}`);
      }
    }
  }

  const allKeys = new Set([...appPermLiterals.values(), ...permissionsLiterals.values()]);
  return { appPermLiterals, permissionsLiterals, allKeys };
}

/**
 * Resolve PERMISSIONS.X / AppPermissions.Y references in a TS source file to concrete keys.
 * Does not harvest arbitrary dotted string literals (avoids i18n / labelKey false positives).
 */
function extractUsedPermissionKeys(source, fa) {
  const cleaned = stripTsComments(source);
  const used = new Set();
  const unknownRefs = [];

  const permRef = /PERMISSIONS\.(\w+)/g;
  let m;
  while ((m = permRef.exec(cleaned))) {
    const key = fa.permissionsLiterals.get(m[1]);
    if (key) used.add(key);
    else unknownRefs.push(`PERMISSIONS.${m[1]}`);
  }

  const appRef = /AppPermissions\.(\w+)/g;
  while ((m = appRef.exec(cleaned))) {
    if (fa.appPermLiterals.has(m[1])) {
      used.add(fa.appPermLiterals.get(m[1]));
    } else {
      unknownRefs.push(`AppPermissions.${m[1]}`);
    }
  }

  return { used, unknownRefs: [...new Set(unknownRefs)] };
}

/**
 * Sidebar catalog: for each item with `permission:`, resolve keys and compare to ROUTE_PERMISSIONS[menuKey].
 */
function extractSidebarPermissions(source, fa) {
  const cleaned = stripTsComments(source);
  const byMenuKey = new Map(); // menuKey → Set of permission keys
  // Match catalog entries roughly: menuKey + optional permission nearby
  const entryRe =
    /\b(\w+)\s*:\s*\{[^{}]*?menuKey:\s*'([^']+)'[^{}]*?(?:permission:\s*([^,}]+))?[^{}]*?\}/gs;
  let m;
  while ((m = entryRe.exec(cleaned))) {
    const menuKey = m[2];
    const permExpr = m[3]?.trim();
    if (!permExpr) continue;
    const keys = new Set();
    const refs = permExpr.matchAll(/PERMISSIONS\.(\w+)|AppPermissions\.(\w+)/g);
    for (const r of refs) {
      if (r[1] && fa.permissionsLiterals.has(r[1])) keys.add(fa.permissionsLiterals.get(r[1]));
      if (r[2] && fa.appPermLiterals.has(r[2])) keys.add(fa.appPermLiterals.get(r[2]));
    }
    if (keys.size > 0) byMenuKey.set(menuKey, keys);
  }
  return byMenuKey;
}

function extractRoutePermissionMap(source, fa) {
  const cleaned = stripTsComments(source);
  const map = new Map(); // path → string[]
  // Lines like: '/path': PERMISSIONS.X,  or '/path': [PERMISSIONS.A, PERMISSIONS.B],
  const lineRe =
    /^\s*(['"])(\/[^'"]*)\1\s*:\s*(\[[^\]]*\]|PERMISSIONS\.\w+|AppPermissions\.\w+|ANY_AUTHENTICATED_PERMISSION)/gm;
  let m;
  while ((m = lineRe.exec(cleaned))) {
    const path = m[2];
    const expr = m[3];
    if (expr === 'ANY_AUTHENTICATED_PERMISSION') {
      map.set(path, []);
      continue;
    }
    const keys = [];
    const refs = expr.matchAll(/PERMISSIONS\.(\w+)|AppPermissions\.(\w+)/g);
    for (const r of refs) {
      if (r[1] && fa.permissionsLiterals.has(r[1])) keys.push(fa.permissionsLiterals.get(r[1]));
      if (r[2] && fa.appPermLiterals.has(r[2])) keys.push(fa.appPermLiterals.get(r[2]));
    }
    map.set(path, keys);
  }
  return map;
}

function normalizeKeySet(arrOrSet) {
  return [...arrOrSet].sort((a, b) => a.localeCompare(b));
}

function sameKeySet(a, b) {
  const as = normalizeKeySet(a).join(',');
  const bs = normalizeKeySet(b).join(',');
  return as === bs;
}

// --- main ---
const backendSrc = read(PATHS.backend);
const faSrc = read(PATHS.faPermissions);
const routeSrc = read(PATHS.routePermissions);
const sidebarSrc = read(PATHS.sidebarRegistry);

if (!existsSync(PATHS.faCatalog)) {
  fail(
    `Missing ${PATHS.faCatalog} — expected re-export of permissions.ts (FA permission catalog entrypoint)`
  );
} else {
  const catalogSrc = read(PATHS.faCatalog);
  if (!catalogSrc.includes("from './permissions'") && !catalogSrc.includes('from "./permissions"')) {
    fail('permissionsCatalog.ts must re-export from ./permissions');
  } else {
    ok('permissionsCatalog.ts re-exports permissions.ts');
  }
}

const backend = extractBackendKeys(backendSrc);
const fa = extractFaCatalog(faSrc);
const route = extractUsedPermissionKeys(routeSrc, fa);
const sidebar = extractUsedPermissionKeys(sidebarSrc, fa);
const routeMap = extractRoutePermissionMap(routeSrc, fa);
const sidebarByMenu = extractSidebarPermissions(sidebarSrc, fa);

console.log('\nPermission key audit');
console.log('────────────────────');
console.log(`Backend AppPermissions.cs:  ${backend.size} keys`);
console.log(`FA permissions.ts:          ${fa.allKeys.size} keys`);
console.log(`routePermissions.ts uses:   ${route.used.size} keys`);
console.log(`adminSidebarRegistry uses:  ${sidebar.used.size} keys`);

// FA ⊆ backend
const faNotInBackend = normalizeKeySet([...fa.allKeys].filter((k) => !backend.has(k)));
if (faNotInBackend.length) {
  fail(`FA catalog keys missing from backend AppPermissions.cs:\n  - ${faNotInBackend.join('\n  - ')}`);
} else {
  ok('All FA catalog keys exist in backend AppPermissions.cs');
}

// Route/sidebar ⊆ FA catalog (and thus backend)
for (const [label, used, unknownRefs] of [
  ['routePermissions.ts', route.used, route.unknownRefs],
  ['adminSidebarRegistry.ts', sidebar.used, sidebar.unknownRefs],
]) {
  if (unknownRefs.length) {
    // AppPermissions refs that aren't in the small FA AppPermissions object —
    // filter those that resolve via string equality in used set already.
    const unresolved = unknownRefs.filter((ref) => {
      const name = ref.split('.')[1];
      return !fa.appPermLiterals.has(name) && !fa.permissionsLiterals.has(name);
    });
    if (unresolved.length) {
      fail(`${label} unresolved constant refs:\n  - ${unresolved.join('\n  - ')}`);
    }
  }
  const missing = normalizeKeySet([...used].filter((k) => !fa.allKeys.has(k)));
  // Raw literals may include non-permission dotted strings; only fail if also not in backend
  const hardMissing = missing.filter((k) => !backend.has(k));
  const notInFa = missing.filter((k) => backend.has(k));
  if (hardMissing.length) {
    fail(`${label} uses unknown permission keys (not in backend):\n  - ${hardMissing.join('\n  - ')}`);
  }
  if (notInFa.length) {
    fail(
      `${label} uses backend keys not in FA permissions.ts (add to PERMISSIONS):\n  - ${notInFa.join('\n  - ')}`
    );
  }
  if (!hardMissing.length && !notInFa.length) {
    ok(`${label} permission keys ⊆ FA catalog`);
  }
}

// Catalog permission must match ROUTE_PERMISSIONS when both declare
let catalogAlignOk = true;
for (const [menuKey, sidebarKeys] of sidebarByMenu) {
  if (!routeMap.has(menuKey)) continue;
  const routeKeys = routeMap.get(menuKey);
  if (routeKeys.length === 0) continue; // ANY_AUTHENTICATED
  if (!sameKeySet(sidebarKeys, routeKeys)) {
    fail(
      `Sidebar catalog permission ≠ ROUTE_PERMISSIONS for ${menuKey}:\n` +
        `  catalog: [${normalizeKeySet(sidebarKeys).join(', ')}]\n` +
        `  route:   [${normalizeKeySet(routeKeys).join(', ')}]`
    );
    catalogAlignOk = false;
  }
}
if (catalogAlignOk) {
  ok('Sidebar catalog permission fields align with ROUTE_PERMISSIONS where both set');
}

// Backend keys missing from FA (informational / optional hard fail)
const backendNotInFa = normalizeKeySet([...backend.keys()].filter((k) => !fa.allKeys.has(k)));
if (backendNotInFa.length) {
  const msg = `FA typed catalog omits ${backendNotInFa.length} backend keys (allowed subset unless --strict-fa-complete)`;
  if (strictFaComplete) {
    fail(`${msg}:\n  - ${backendNotInFa.join('\n  - ')}`);
  } else {
    console.log(`ℹ ${msg}`);
  }
} else {
  ok('FA catalog covers every backend key');
}

// Suspicious naming drift (warn only — historical keys are intentional)
const namingNotes = [];
for (const key of backend.keys()) {
  if (key.startsWith('users.') || key.startsWith('cashregister.')) {
    namingNotes.push(`${key} looks like a plural/legacy alias — canonical is singular / cash_register.*`);
  }
  if (key === 'dailyclosing.view' || key === 'tagesabschluss.view') {
    namingNotes.push(`${key} is non-canonical; use daily-closing.view`);
  }
}
if (namingNotes.length) {
  for (const n of namingNotes) fail(n);
} else {
  ok('No plural/legacy alias keys (users.*, cashregister.*, dailyclosing.*) in backend');
}

if (wantTable) {
  const union = new Set([...backend.keys(), ...fa.allKeys, ...route.used, ...sidebar.used]);
  console.log('\nBackend Key\tFA Catalog\tRoute\tMenu');
  for (const key of normalizeKeySet(union)) {
    const b = backend.has(key) ? key : '—';
    const f = fa.allKeys.has(key) ? key : '—';
    const r = route.used.has(key) ? key : '—';
    const m = sidebar.used.has(key) ? key : '—';
    console.log(`${b}\t${f}\t${r}\t${m}`);
  }
} else {
  // Compact highlight for daily-closing (regression of the Manager menu bug)
  console.log('\nDaily closing mapping:');
  for (const key of ['daily-closing.view', 'daily-closing.execute']) {
    console.log(
      `  ${key}\tFA=${fa.allKeys.has(key)}\tRoute=${route.used.has(key)}\tMenu=${sidebar.used.has(key)}\tBackend=${backend.has(key)}`
    );
  }
}

if (process.exitCode) {
  console.error('\nPermission key audit FAILED');
  process.exit(1);
}
console.log('\nPermission key audit OK');
