/**
 * Persist personal + tenant-shared permission audit filter presets (localStorage).
 * Shared presets are visible to other admins on the same browser/tenant profile;
 * cross-device sharing uses clipboard export/import.
 */

import type { PermissionAuditFilterState } from '@/features/users/utils/permissionAuditFilters';

export type SavedPermissionAuditFilter = {
  id: string;
  name: string;
  /** When true, stored in tenant shared list. */
  shared: boolean;
  filters: PermissionAuditFilterState;
  createdAt: string;
  createdByUserId?: string | null;
  createdByName?: string | null;
};

const PERSONAL_PREFIX = 'fa_permission_audit_filters_personal_v1:';
const SHARED_PREFIX = 'fa_permission_audit_filters_shared_v1:';

function personalKey(userId: string): string {
  return `${PERSONAL_PREFIX}${userId}`;
}

function sharedKey(tenantId: string): string {
  return `${SHARED_PREFIX}${tenantId || 'default'}`;
}

function readList(storageKey: string): SavedPermissionAuditFilter[] {
  if (typeof window === 'undefined') return [];
  try {
    const raw = window.localStorage.getItem(storageKey);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return [];
    return parsed.filter(isSavedFilter);
  } catch {
    return [];
  }
}

function writeList(storageKey: string, items: SavedPermissionAuditFilter[]): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(storageKey, JSON.stringify(items));
  } catch {
    // ignore quota / private mode
  }
}

function isSavedFilter(value: unknown): value is SavedPermissionAuditFilter {
  if (!value || typeof value !== 'object') return false;
  const v = value as SavedPermissionAuditFilter;
  return (
    typeof v.id === 'string' &&
    typeof v.name === 'string' &&
    typeof v.shared === 'boolean' &&
    typeof v.createdAt === 'string' &&
    v.filters != null &&
    typeof v.filters === 'object' &&
    Array.isArray(v.filters.quickFilters)
  );
}

export function loadPersonalPermissionAuditFilters(userId: string): SavedPermissionAuditFilter[] {
  if (!userId) return [];
  return readList(personalKey(userId));
}

export function loadSharedPermissionAuditFilters(tenantId: string): SavedPermissionAuditFilter[] {
  return readList(sharedKey(tenantId));
}

export function savePermissionAuditFilter(
  filter: SavedPermissionAuditFilter,
  opts: { userId: string; tenantId: string }
): SavedPermissionAuditFilter[] {
  if (filter.shared) {
    const list = loadSharedPermissionAuditFilters(opts.tenantId);
    const next = [...list.filter((f) => f.id !== filter.id), filter];
    writeList(sharedKey(opts.tenantId), next);
    return next;
  }
  const list = loadPersonalPermissionAuditFilters(opts.userId);
  const next = [...list.filter((f) => f.id !== filter.id), filter];
  writeList(personalKey(opts.userId), next);
  return next;
}

export function deletePermissionAuditFilter(
  id: string,
  opts: { userId: string; tenantId: string; shared: boolean }
): void {
  if (opts.shared) {
    writeList(
      sharedKey(opts.tenantId),
      loadSharedPermissionAuditFilters(opts.tenantId).filter((f) => f.id !== id)
    );
    return;
  }
  writeList(
    personalKey(opts.userId),
    loadPersonalPermissionAuditFilters(opts.userId).filter((f) => f.id !== id)
  );
}

export function encodePermissionAuditFilterShare(filter: SavedPermissionAuditFilter): string {
  return btoa(unescape(encodeURIComponent(JSON.stringify(filter))));
}

export function decodePermissionAuditFilterShare(
  token: string
): SavedPermissionAuditFilter | null {
  try {
    const json = decodeURIComponent(escape(atob(token.trim())));
    const parsed = JSON.parse(json) as unknown;
    return isSavedFilter(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

export function createSavedPermissionAuditFilterId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }
  return `paf_${Date.now()}_${Math.random().toString(36).slice(2, 10)}`;
}
