/** GET /api/admin/activities has no type filter; this is the list ceiling (pagination is separate). */
export const MONATSBELEG_ACTIVITY_LIST_LIMIT = 50;

export const MONATSBELEG_MANAGER_CONTACTED_TYPE = 'MonatsbelegManagerContacted';

export const MONATSBELEG_MANAGER_CONTACTED_WINDOW_MS = 7 * 24 * 60 * 60 * 1000;

/**
 * Count cashier→Manager Monatsbeleg contact events on or after `sinceUtc`.
 * Ignores malformed timestamps and non-matching types.
 */
export function countMonatsbelegManagerContactedSince(
  items: readonly { type?: string | null; createdAtUtc?: string | null }[] | null | undefined,
  sinceUtc: Date
): number {
  if (!items?.length) return 0;
  const sinceMs = sinceUtc.getTime();
  if (Number.isNaN(sinceMs)) return 0;

  let count = 0;
  for (const item of items) {
    if (item.type !== MONATSBELEG_MANAGER_CONTACTED_TYPE) continue;
    const createdMs = Date.parse(item.createdAtUtc ?? '');
    if (Number.isNaN(createdMs) || createdMs < sinceMs) continue;
    count += 1;
  }
  return count;
}

export function monatsbelegManagerContactedSinceUtc(now: Date = new Date()): Date {
  return new Date(now.getTime() - MONATSBELEG_MANAGER_CONTACTED_WINDOW_MS);
}
