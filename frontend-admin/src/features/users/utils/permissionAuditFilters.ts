/**
 * Client-side matching helpers for permission audit filters.
 */

import type { PermissionAuditEntry } from '@/features/audit/api/permissionAudit';

/** High-impact permission key prefixes / exact keys for "critical" quick filter. */
const CRITICAL_PERMISSION_PATTERNS: readonly RegExp[] = [
  /^payment\./i,
  /^tse\./i,
  /^rksv\./i,
  /^backup\./i,
  /^settings\.manage$/i,
  /^users\.manage$/i,
  /^role\./i,
  /^system\./i,
  /^audit\./i,
  /^license\./i,
  /^cashregister\./i,
];

export type PermissionAuditQuickFilter = 'last24h' | 'onlyMine' | 'onlyCritical';

export type PermissionAuditFilterState = {
  fromDate?: string;
  toDate?: string;
  actorUserId?: string;
  permissionKey?: string;
  roleName?: string;
  roleId?: string;
  action?: PermissionAuditEntry['action'] | 'all';
  search?: string;
  quickFilters: PermissionAuditQuickFilter[];
};

export function isCriticalPermissionKey(key: string | null | undefined): boolean {
  const k = (key ?? '').trim();
  if (!k) return false;
  return CRITICAL_PERMISSION_PATTERNS.some((re) => re.test(k));
}

export function isCriticalPermissionAuditEntry(entry: PermissionAuditEntry): boolean {
  if (entry.action === 'created' || entry.action === 'deleted') return true;
  if (isCriticalPermissionKey(entry.permissionKey)) return true;
  const oldV = (entry.oldValue ?? '').toLowerCase();
  const newV = (entry.newValue ?? '').toLowerCase();
  // Removal of a previously allowed permission
  if (
    (oldV === 'allowed' || oldV === 'individual') &&
    (newV === 'absent' || newV === 'denied' || newV === '')
  ) {
    return true;
  }
  return false;
}

export function matchesPermissionAuditFreeText(
  entry: PermissionAuditEntry,
  query: string
): boolean {
  const q = query.trim().toLowerCase();
  if (!q) return true;
  const hay = [
    entry.permissionKey,
    entry.roleName,
    entry.actorName,
    entry.actorEmail,
    entry.reason,
  ]
    .filter(Boolean)
    .join(' ')
    .toLowerCase();
  return hay.includes(q);
}

export function applyClientPermissionAuditFilters(
  entries: readonly PermissionAuditEntry[],
  opts: {
    search?: string;
    action?: PermissionAuditEntry['action'] | 'all';
    onlyCritical?: boolean;
    onlyActorUserId?: string;
    sinceIso?: string;
  }
): PermissionAuditEntry[] {
  let list = [...entries];
  if (opts.action && opts.action !== 'all') {
    list = list.filter((e) => e.action === opts.action);
  }
  if (opts.search?.trim()) {
    list = list.filter((e) => matchesPermissionAuditFreeText(e, opts.search!));
  }
  if (opts.onlyCritical) {
    list = list.filter(isCriticalPermissionAuditEntry);
  }
  if (opts.onlyActorUserId) {
    list = list.filter((e) => e.actorUserId === opts.onlyActorUserId);
  }
  if (opts.sinceIso) {
    const since = new Date(opts.sinceIso).getTime();
    if (!Number.isNaN(since)) {
      list = list.filter((e) => {
        const ts = new Date(e.timestamp).getTime();
        return !Number.isNaN(ts) && ts >= since;
      });
    }
  }
  return list;
}
