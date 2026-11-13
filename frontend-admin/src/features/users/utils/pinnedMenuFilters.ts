/**
 * Persist pinned menu filter keys for the permission UI.
 */
const STORAGE_KEY = 'fa_pinned_menu_filters_v1';

export function loadPinnedMenuFilters(): string[] {
  if (typeof window === 'undefined') return [];
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return [];
    return parsed.filter((x): x is string => typeof x === 'string' && x.length > 0);
  } catch {
    return [];
  }
}

export function savePinnedMenuFilters(keys: readonly string[]): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify([...keys]));
  } catch {
    // ignore quota / private mode
  }
}

export function togglePinnedMenuFilter(menuKey: string, pinned: readonly string[]): string[] {
  const set = new Set(pinned);
  if (set.has(menuKey)) set.delete(menuKey);
  else set.add(menuKey);
  const next = [...set];
  savePinnedMenuFilters(next);
  return next;
}
