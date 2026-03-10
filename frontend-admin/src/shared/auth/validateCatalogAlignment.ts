/**
 * Validates that permission keys used in menu and route configs exist in the permission catalog.
 * Logs console warnings for keys that are used in UI but not in catalog (e.g. backend contract drift).
 */

import { MENU_PERMISSION } from './menuPermissions';
import { ROUTE_PERMISSIONS } from './routePermissions';

function collectKeysFromMap(
  map: Record<string, string | string[] | undefined>
): Set<string> {
  const keys = new Set<string>();
  for (const value of Object.values(map)) {
    if (value == null) continue;
    const arr = Array.isArray(value) ? value : [value];
    for (const p of arr) if (typeof p === 'string' && p) keys.add(p);
  }
  return keys;
}

export interface CatalogAlignmentResult {
  /** Permission keys used in menu/route but not present in catalog. */
  unknownKeys: string[];
  /** Whether any warnings were logged. */
  hasWarnings: boolean;
}

/**
 * Compares menu and route permission keys against the catalog.
 * If a key is used in MENU_PERMISSION or ROUTE_PERMISSIONS but not in catalogKeys, logs a console warning.
 * @param catalogKeys – permission keys from GET /api/UserManagement/roles/permissions-catalog (e.g. item.key)
 * @param options.warnUnknown – if true (default), console.warn for each unknown key
 */
export function validateCatalogAlignment(
  catalogKeys: string[] | Set<string>,
  options: { warnUnknown?: boolean } = {}
): CatalogAlignmentResult {
  const catalogSet = catalogKeys instanceof Set ? catalogKeys : new Set(catalogKeys);
  const menuKeys = collectKeysFromMap(MENU_PERMISSION);
  const routeKeys = collectKeysFromMap(ROUTE_PERMISSIONS as Record<string, string | string[] | undefined>);
  const usedKeys = new Set<string>([...menuKeys, ...routeKeys]);
  const unknownKeys: string[] = [];
  for (const key of usedKeys) {
    if (!catalogSet.has(key)) unknownKeys.push(key);
  }
  const { warnUnknown = true } = options;
  if (warnUnknown && unknownKeys.length > 0) {
    console.warn(
      '[validateCatalogAlignment] Menu/route permission keys not found in catalog (check backend contract):',
      unknownKeys
    );
  }
  return { unknownKeys, hasWarnings: unknownKeys.length > 0 };
}
