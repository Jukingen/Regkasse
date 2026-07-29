'use client';

import { useMemo } from 'react';

import {
  isLicenseLockdownSidebarActive,
  shouldHideSidebarKeyForLicenseLockdown,
  type SidebarLicenseLockdownState,
} from '@/shared/sidebarLicenseLockdown';
import {
  LOCKDOWN_MENU_ALIAS_TO_KEYS,
  LOCKDOWN_VISIBLE_MENUS,
  type LockdownVisibleMenuKey,
} from '@/shared/licenseMenuConfig';
import { useLicenseStatus, type LicenseLifecycleUiState } from '@/hooks/useLicenseStatus';
import { usePermissions } from '@/hooks/usePermissions';

/** Sketch-friendly aliases — sourced from `licenseMenuConfig`. */
export const LICENSE_LOCKDOWN_VISIBLE_MENU_ALIASES = LOCKDOWN_VISIBLE_MENUS.map(
  (entry) => entry.key
) as unknown as readonly LockdownVisibleMenuKey[];

export type LicenseLockdownMenuAlias = LockdownVisibleMenuKey;

const ALLOWED_ACTIONS_EXACT = new Set([
  'license-renew',
  'license-extend',
  'data-export',
  'account-closure',
  'account-close',
]);

/**
 * Whether a sidebar key/path/alias remains visible under Locked/Archived.
 * Super Admin bypasses lockdown filtering.
 */
export function isLicenseLockdownMenuVisible(
  menuKey: string,
  options: {
    isLocked: boolean;
    isSuperAdmin?: boolean;
  }
): boolean {
  if (!options.isLocked || options.isSuperAdmin) {
    return true;
  }

  const normalized = menuKey.trim();
  if (!normalized) return false;

  const candidates =
    (LOCKDOWN_MENU_ALIAS_TO_KEYS as Record<string, readonly string[] | undefined>)[
      normalized
    ] ?? [normalized];

  return candidates.some((key) => !shouldHideSidebarKeyForLicenseLockdown(key));
}

/**
 * Write vs read action gate for Locked/Archived (complements `useLicenseGuard`).
 */
export function isLicenseLockdownActionAllowed(
  action: string,
  options: {
    isLocked: boolean;
    isSuperAdmin?: boolean;
  }
): boolean {
  if (!options.isLocked || options.isSuperAdmin) {
    return true;
  }

  const normalized = action.trim().toLowerCase();
  if (!normalized) return false;

  if (ALLOWED_ACTIONS_EXACT.has(normalized)) {
    return true;
  }

  return (
    normalized.startsWith('view') ||
    normalized.startsWith('read') ||
    normalized.startsWith('get') ||
    normalized.startsWith('list') ||
    normalized.startsWith('export')
  );
}

/**
 * React hook for FA license lockdown menu / action visibility.
 * Sidebar tree filtering remains in `filterSidebarMenuItemsForLicenseLockdown` (AdminSidebar);
 * this hook is the shared API for feature UIs and the restricted-mode footer.
 */
export function useLicenseMenuVisibility() {
  const { status } = useLicenseStatus();
  const { isSuperAdmin, hasPermission } = usePermissions();

  const licenseState: LicenseLifecycleUiState | null = status?.state ?? null;
  const isLocked = isLicenseLockdownSidebarActive(licenseState);

  return useMemo(() => {
    const opts = { isLocked, isSuperAdmin };

    return {
      isLocked,
      licenseState: licenseState as SidebarLicenseLockdownState | LicenseLifecycleUiState | null,
      isSuperAdmin,
      /** Permission helper passthrough (callers often need both). */
      hasPermission,
      isMenuVisible: (menuKey: string) => isLicenseLockdownMenuVisible(menuKey, opts),
      isActionAllowed: (action: string) => isLicenseLockdownActionAllowed(action, opts),
      visibleMenus: LICENSE_LOCKDOWN_VISIBLE_MENU_ALIASES,
      lockdownMenus: LOCKDOWN_VISIBLE_MENUS,
    };
  }, [hasPermission, isLocked, isSuperAdmin, licenseState]);
}
