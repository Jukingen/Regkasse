import { getCatalog, type TextLocale, SUPPORTED_TEXT_LOCALES } from '@/i18n/config';

import { permissionCatalogGroupToSlug } from './permissionCatalogGroup';
import { permissionCodeToLabelLeaf } from './permissionDisplayLabel';

export type LocaleLabels = {
  de: string;
  en: string;
  tr: string;
};

/**
 * Multi-language search row for one catalog permission.
 * Labels are sourced from `users.roleDrawer.permissionLabels` / `.groups`
 * in de / en / tr catalogs (there is no separate permissions.json namespace).
 */
export interface PermissionSearchIndex {
  key: string;
  labels: LocaleLabels;
  /** Catalog group slug (e.g. tagesabschluss). */
  group: string;
  groupLabels: LocaleLabels;
}

export type PermissionSearchableItem = {
  key: string;
  /** Backend / catalog group label (often German); used as fallback + slug source. */
  group?: string | null;
};

type UsersRoleDrawerSlice = {
  groups?: Record<string, string>;
  permissionLabels?: Record<string, string>;
};

function readUsersRoleDrawer(locale: TextLocale): UsersRoleDrawerSlice {
  const users = getCatalog(locale).users as {
    roleDrawer?: UsersRoleDrawerSlice;
  };
  return users.roleDrawer ?? {};
}

function emptyLabels(): LocaleLabels {
  return { de: '', en: '', tr: '' };
}

function labelFor(
  slice: UsersRoleDrawerSlice,
  leaf: string,
  fallback: string
): string {
  return slice.permissionLabels?.[leaf]?.trim() || fallback;
}

function groupLabelFor(
  slice: UsersRoleDrawerSlice,
  groupSlug: string,
  fallback: string
): string {
  return slice.groups?.[groupSlug]?.trim() || fallback;
}

/**
 * Builds structured multi-language search entries from the permission catalog
 * and FA i18n (`users.roleDrawer` in de/en/tr).
 */
export function buildPermissionSearchEntries(
  items: readonly PermissionSearchableItem[]
): PermissionSearchIndex[] {
  const slices = Object.fromEntries(
    SUPPORTED_TEXT_LOCALES.map((locale) => [locale, readUsersRoleDrawer(locale)])
  ) as Record<TextLocale, UsersRoleDrawerSlice>;

  return items.map((item) => {
    const leaf = permissionCodeToLabelLeaf(item.key);
    const groupSlug = permissionCatalogGroupToSlug(item.group?.trim() || 'Other');
    const rawGroup = item.group?.trim() || groupSlug.replace(/_/g, ' ');

    const labels: LocaleLabels = {
      de: labelFor(slices.de, leaf, item.key),
      en: labelFor(slices.en, leaf, item.key),
      tr: labelFor(slices.tr, leaf, item.key),
    };

    const groupLabels: LocaleLabels = {
      de: groupLabelFor(slices.de, groupSlug, rawGroup),
      en: groupLabelFor(slices.en, groupSlug, rawGroup),
      tr: groupLabelFor(slices.tr, groupSlug, rawGroup),
    };

    return {
      key: item.key,
      labels,
      group: groupSlug,
      groupLabels,
    };
  });
}

/**
 * @deprecated Prefer {@link buildPermissionSearchEntries}. Kept for callers that need Map haystacks.
 */
export function buildPermissionSearchIndex(
  items: readonly PermissionSearchableItem[]
): Map<string, string[]> {
  const map = new Map<string, string[]>();
  for (const entry of buildPermissionSearchEntries(items)) {
    map.set(entry.key, [
      entry.key,
      entry.labels.de,
      entry.labels.en,
      entry.labels.tr,
      entry.group,
      entry.groupLabels.de,
      entry.groupLabels.en,
      entry.groupLabels.tr,
    ].filter(Boolean));
  }
  return map;
}

function matchesQuery(
  entry: PermissionSearchIndex,
  normalizedQuery: string,
  language: TextLocale | 'all'
): boolean {
  if (entry.key.toLowerCase().includes(normalizedQuery)) return true;

  if (language === 'all') {
    return (
      entry.labels.de.toLowerCase().includes(normalizedQuery) ||
      entry.labels.en.toLowerCase().includes(normalizedQuery) ||
      entry.labels.tr.toLowerCase().includes(normalizedQuery) ||
      entry.groupLabels.de.toLowerCase().includes(normalizedQuery) ||
      entry.groupLabels.en.toLowerCase().includes(normalizedQuery) ||
      entry.groupLabels.tr.toLowerCase().includes(normalizedQuery) ||
      entry.group.toLowerCase().includes(normalizedQuery)
    );
  }

  const label = (entry.labels[language] || '').toLowerCase();
  const group = (entry.groupLabels[language] || '').toLowerCase();
  return label.includes(normalizedQuery) || group.includes(normalizedQuery);
}

/**
 * Filters permission search entries.
 * Default `language: 'all'` so a query in any UI language matches DE/EN/TR labels.
 */
export function searchPermissions(
  allPermissions: readonly PermissionSearchIndex[],
  query: string,
  language: TextLocale | 'all' = 'all'
): PermissionSearchIndex[] {
  const normalizedQuery = query.toLowerCase().trim();
  if (!normalizedQuery) return [...allPermissions];
  return allPermissions.filter((p) => matchesQuery(p, normalizedQuery, language));
}

/** Case-insensitive match for a single permission key against a search index Map. */
export function permissionMatchesSearch(
  permissionKey: string,
  searchTerm: string,
  index: Map<string, string[]>
): boolean {
  const q = searchTerm.trim().toLowerCase();
  if (!q) return true;
  const haystack = index.get(permissionKey);
  if (!haystack?.length) return permissionKey.toLowerCase().includes(q);
  return haystack.some((part) => part.toLowerCase().includes(q));
}

export function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

export const PERMISSION_SEARCH_DEBOUNCE_MS = 300;
