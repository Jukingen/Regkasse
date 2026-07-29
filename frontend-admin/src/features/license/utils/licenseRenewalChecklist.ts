/** Mandant license renewal preparation checklist (localStorage, per tenant). */

export const LICENSE_RENEWAL_CHECKLIST_ITEM_IDS = [
  'reviewLicenseData',
  'prepareLicenseKey',
  'verifyPaymentMethod',
  'updateContactData',
  'contactSupportIfNeeded',
] as const;

export type LicenseRenewalChecklistItemId =
  (typeof LICENSE_RENEWAL_CHECKLIST_ITEM_IDS)[number];

const STORAGE_PREFIX = 'regkasse.license.renewalChecklist.';

function storageKey(tenantId: string): string {
  return `${STORAGE_PREFIX}${tenantId}`;
}

function canUseLocalStorage(): boolean {
  return typeof globalThis.localStorage !== 'undefined';
}

export function loadLicenseRenewalChecklistCompleted(
  tenantId: string
): Set<LicenseRenewalChecklistItemId> {
  const completed = new Set<LicenseRenewalChecklistItemId>();
  if (!canUseLocalStorage() || !tenantId.trim()) return completed;

  try {
    const raw = globalThis.localStorage.getItem(storageKey(tenantId));
    if (!raw?.trim()) return completed;
    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed)) return completed;
    for (const id of parsed) {
      if (
        typeof id === 'string' &&
        (LICENSE_RENEWAL_CHECKLIST_ITEM_IDS as readonly string[]).includes(id)
      ) {
        completed.add(id as LicenseRenewalChecklistItemId);
      }
    }
  } catch {
    // ignore corrupt storage
  }
  return completed;
}

export function saveLicenseRenewalChecklistCompleted(
  tenantId: string,
  completed: ReadonlySet<LicenseRenewalChecklistItemId>
): void {
  if (!canUseLocalStorage() || !tenantId.trim()) return;
  try {
    globalThis.localStorage.setItem(
      storageKey(tenantId),
      JSON.stringify([...completed])
    );
  } catch {
    // ignore quota / private mode
  }
}

export function toggleLicenseRenewalChecklistItem(
  completed: ReadonlySet<LicenseRenewalChecklistItemId>,
  itemId: LicenseRenewalChecklistItemId
): Set<LicenseRenewalChecklistItemId> {
  const next = new Set(completed);
  if (next.has(itemId)) next.delete(itemId);
  else next.add(itemId);
  return next;
}

export function getLicenseRenewalChecklistProgressPercent(
  completedCount: number,
  totalCount: number
): number {
  if (!Number.isFinite(completedCount) || !Number.isFinite(totalCount) || totalCount <= 0) {
    return 0;
  }
  return Math.min(100, Math.max(0, Math.round((completedCount / totalCount) * 100)));
}
