/**
 * Mirrors backend shift occupancy (`CashRegister.CurrentUserId`): who currently holds the open till.
 * Distinct from admin assignment (`assignedUserId`).
 */
export function isOpenShiftHeldBy(
  currentUserId: string | null | undefined,
  actorUserId: string | null | undefined
): boolean {
  const holder = currentUserId?.trim();
  const actor = actorUserId?.trim();
  return Boolean(holder && actor && holder === actor);
}

type OpenShiftHolderSource = {
  currentCashierName?: string | null;
  currentUser?: { userName?: string | null } | null;
  currentUserId?: string | null;
};

/** Display name for the current till holder, for force-close confirm copy. */
export function resolveOpenShiftHolderName(register: OpenShiftHolderSource): string {
  return (
    register.currentCashierName?.trim() ||
    register.currentUser?.userName?.trim() ||
    register.currentUserId?.trim() ||
    ''
  );
}
