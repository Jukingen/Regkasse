export const RECENT_TAX_GROUPS_STORAGE_KEY = 'fa_recent_tax_groups_v1';
export const RECENT_TAX_GROUPS_MAX = 5;

function isBrowser(): boolean {
  return typeof window !== 'undefined' && typeof window.localStorage !== 'undefined';
}

function storageKey(tenantId: string | null | undefined): string {
  const tid = tenantId?.trim() || 'default';
  return `${RECENT_TAX_GROUPS_STORAGE_KEY}:${tid}`;
}

/** Recently used tax group ids (most recent first), scoped by tenant. */
export function readRecentTaxGroupIds(tenantId?: string | null): string[] {
  if (!isBrowser()) return [];
  try {
    const raw = window.localStorage.getItem(storageKey(tenantId));
    if (!raw) return [];
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return [];
    return parsed
      .filter((id): id is string => typeof id === 'string' && id.length > 0)
      .slice(0, RECENT_TAX_GROUPS_MAX);
  } catch {
    return [];
  }
}

export function pushRecentTaxGroupId(taxGroupId: string, tenantId?: string | null): string[] {
  if (!isBrowser() || !taxGroupId) return readRecentTaxGroupIds(tenantId);
  const prev = readRecentTaxGroupIds(tenantId);
  const updated = [taxGroupId, ...prev.filter((id) => id !== taxGroupId)].slice(
    0,
    RECENT_TAX_GROUPS_MAX
  );
  window.localStorage.setItem(storageKey(tenantId), JSON.stringify(updated));
  return updated;
}
