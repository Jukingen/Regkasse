'use client';

/**
 * Logical menu-area visibility from `menuPermissionRegistry`.
 *
 * Prefer this for IA keys (`tagesabschluss`, `products`, …). Path-level sidebar leaves
 * still use `ROUTE_PERMISSIONS` via `isMenuItemAllowed` when no registry mapping exists.
 * Admin sidebar tree filtering uses the same registry via `tryRegistryMenuVisibility`.
 *
 * @example
 * const { visible } = useMenuPermissions('tagesabschluss');
 * if (!visible) return null;
 */
import {
  MENU_PERMISSIONS,
  type MenuAreaKey,
  type MenuPermissionState,
  resolveMenuAreaKey,
  resolveMenuAreaPermissions,
} from '@/shared/auth/menuPermissionRegistry';

import { usePermissions } from './usePermissions';

export type { MenuPermissionState };

/**
 * @param menuKey Registry area (`tagesabschluss`), catalog id, or primary path (`/tagesabschluss`)
 */
export function useMenuPermissions(menuKey: MenuAreaKey | string): MenuPermissionState {
  const { hasPermission, hasAnyPermission, userPermissions } = usePermissions();

  const area = resolveMenuAreaKey(String(menuKey));
  if (!area) {
    return { visible: false, permission: '' };
  }

  const required = resolveMenuAreaPermissions(area);
  const permission = required[0] ?? '';
  const entry = MENU_PERMISSIONS[area];

  if (required.length === 1) {
    if (hasPermission(required[0]!)) {
      return { visible: true, permission };
    }
  } else if (required.length > 1) {
    if (hasAnyPermission([...required])) {
      return { visible: true, permission };
    }
  }

  if (entry.fallback || required.length === 0) {
    return { visible: userPermissions.length > 0, permission };
  }

  return { visible: false, permission };
}
