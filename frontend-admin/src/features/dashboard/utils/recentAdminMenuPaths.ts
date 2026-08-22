import { SIDEBAR_NAV_ITEM_CATALOG } from '@/shared/adminSidebarRegistry';
import { isCashRegisterDetailPath, KASSENVERWALTUNG_PATH } from '@/shared/cashRegisterRoutes';

export const RECENT_ADMIN_MENU_STORAGE_KEY = 'rk_admin_recent_menu_paths';
export const MAX_RECENT_ADMIN_MENU_PATHS = 4;
export const RECENT_ADMIN_MENU_CHANGED_EVENT = 'rk-admin-recent-menu';

const SKIP_RECENT_PATHS = new Set([
  '/403',
  '/login',
  '/force-password-change',
  '/dashboard',
  '/impersonate-callback',
  '/settings/password',
]);

function normalizePath(pathname: string): string {
  return pathname.replace(/\/$/, '') || '/';
}

function catalogMenuKeysLongestFirst(): string[] {
  return Object.values(SIDEBAR_NAV_ITEM_CATALOG)
    .map((item) => item.menuKey)
    .filter((key) => Boolean(key) && key !== '/dashboard')
    .sort((a, b) => b.length - a.length);
}

/** Maps a visited URL to a sidebar leaf key, or null when it should not be remembered. */
export function resolveRecentMenuStorageKey(pathname: string): string | null {
  const normalized = normalizePath(pathname);
  if (SKIP_RECENT_PATHS.has(normalized) || normalized.startsWith('/login/')) {
    return null;
  }

  if (isCashRegisterDetailPath(normalized)) {
    return KASSENVERWALTUNG_PATH;
  }

  for (const key of catalogMenuKeysLongestFirst()) {
    if (normalized === key || normalized.startsWith(`${key}/`)) {
      return key;
    }
  }

  return null;
}

export function getSidebarLabelKeyForPath(path: string): string | undefined {
  const key = normalizePath(path);
  return Object.values(SIDEBAR_NAV_ITEM_CATALOG).find((item) => item.menuKey === key)?.labelKey;
}

function parseStoredPaths(raw: string | null): string[] {
  if (!raw) {
    return [];
  }
  try {
    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed)) {
      return [];
    }
    return parsed
      .filter((item): item is string => typeof item === 'string' && item.startsWith('/'))
      .map(normalizePath)
      .slice(0, MAX_RECENT_ADMIN_MENU_PATHS);
  } catch {
    return [];
  }
}

export function readRecentAdminMenuPaths(): string[] {
  if (typeof window === 'undefined') {
    return [];
  }
  return parseStoredPaths(window.localStorage.getItem(RECENT_ADMIN_MENU_STORAGE_KEY));
}

function emitRecentMenuChanged(): void {
  if (typeof window === 'undefined') {
    return;
  }
  window.dispatchEvent(new Event(RECENT_ADMIN_MENU_CHANGED_EVENT));
}

/** Most-recent-first, unique, capped at {@link MAX_RECENT_ADMIN_MENU_PATHS}. */
export function rememberRecentAdminMenuPath(pathname: string): void {
  if (typeof window === 'undefined') {
    return;
  }
  const key = resolveRecentMenuStorageKey(pathname);
  if (!key) {
    return;
  }
  const next = [key, ...readRecentAdminMenuPaths().filter((item) => item !== key)].slice(
    0,
    MAX_RECENT_ADMIN_MENU_PATHS
  );
  window.localStorage.setItem(RECENT_ADMIN_MENU_STORAGE_KEY, JSON.stringify(next));
  emitRecentMenuChanged();
}

export function subscribeRecentAdminMenuPaths(onStoreChange: () => void): () => void {
  if (typeof window === 'undefined') {
    return () => undefined;
  }
  window.addEventListener(RECENT_ADMIN_MENU_CHANGED_EVENT, onStoreChange);
  window.addEventListener('storage', onStoreChange);
  return () => {
    window.removeEventListener(RECENT_ADMIN_MENU_CHANGED_EVENT, onStoreChange);
    window.removeEventListener('storage', onStoreChange);
  };
}
