/**
 * Pure sidebar-favorites helpers (cap, sanitize, toggle, prune, reorder).
 */

import type { MenuProps } from 'antd';
import { arrayMove } from '@dnd-kit/sortable';

import { MAX_SIDEBAR_FAVORITES } from '@/features/menu/menuConfig';

export type AddFavoriteResult = 'added' | 'exists' | 'max';

const MENU_KEY_PATTERN = /^(?:\/|grp-|rksv-grp-)/;

export function isSidebarFavoriteMenuKey(value: unknown): value is string {
  return typeof value === 'string' && value.length > 0 && MENU_KEY_PATTERN.test(value);
}

export function sanitizeFavoriteMenuKeys(
  ids: unknown,
  max = MAX_SIDEBAR_FAVORITES
): string[] {
  if (!Array.isArray(ids)) return [];
  const seen = new Set<string>();
  const out: string[] = [];
  for (const id of ids) {
    if (!isSidebarFavoriteMenuKey(id) || seen.has(id)) continue;
    seen.add(id);
    out.push(id);
    if (out.length >= max) break;
  }
  return out;
}

export function collectAllMenuKeys(items: MenuProps['items'] | undefined): string[] {
  const out: string[] = [];
  const walk = (list: MenuProps['items'] | undefined) => {
    if (!list) return;
    for (const it of list) {
      if (!it || typeof it !== 'object') continue;
      if ('type' in it && it.type === 'divider') continue;
      const node = it as { key?: string | number; children?: MenuProps['items'] };
      if (typeof node.key === 'string' && node.key.length > 0) {
        out.push(node.key);
      }
      if (node.children && node.children.length > 0) {
        walk(node.children);
      }
    }
  };
  walk(items);
  return out;
}

/**
 * Drop favorites that are no longer in the permission-filtered menu tree.
 * When `visibleMenuKeys` is empty, skip pruning so a first paint cannot wipe seeded defaults.
 */
export function pruneInaccessibleFavorites(
  favoriteIds: readonly string[],
  visibleMenuKeys: ReadonlySet<string>
): string[] {
  if (visibleMenuKeys.size === 0) {
    return [...favoriteIds];
  }
  return favoriteIds.filter((id) => visibleMenuKeys.has(id));
}

export function addFavoriteMenuKey(
  favoriteIds: readonly string[],
  menuKey: string,
  max = MAX_SIDEBAR_FAVORITES
): { ids: string[]; result: AddFavoriteResult } {
  if (!isSidebarFavoriteMenuKey(menuKey)) {
    return { ids: [...favoriteIds], result: 'exists' };
  }
  if (favoriteIds.includes(menuKey)) {
    return { ids: [...favoriteIds], result: 'exists' };
  }
  if (favoriteIds.length >= max) {
    return { ids: [...favoriteIds], result: 'max' };
  }
  return { ids: [...favoriteIds, menuKey], result: 'added' };
}

export function removeFavoriteMenuKey(
  favoriteIds: readonly string[],
  menuKey: string
): string[] {
  return favoriteIds.filter((id) => id !== menuKey);
}

export function reorderFavoriteMenuKeys(
  favoriteIds: readonly string[],
  activeId: string,
  overId: string
): string[] | null {
  if (activeId === overId) return null;
  const oldIndex = favoriteIds.indexOf(activeId);
  const newIndex = favoriteIds.indexOf(overId);
  if (oldIndex < 0 || newIndex < 0) return null;
  return arrayMove([...favoriteIds], oldIndex, newIndex);
}
