/**
 * Client-side cash-register assignment visibility (documentation / tests).
 * POS must not re-apply this to GET /api/pos/cash-register/selectable — the API already
 * filters via CashRegisterAssignment.IsVisibleTo. A second pass can empty the picker
 * when user.id and assignedUserId differ (GUID case / claim vs Identity id).
 * Null/missing assignedUserId = shared register (visible to every POS user).
 */

export type PosAssignmentUser = {
  id?: string;
  role?: string;
  roles?: string[];
};

function hasNamedRole(user: PosAssignmentUser, role: string): boolean {
  if (user.role === role) return true;
  return (user.roles ?? []).some((r) => r === role);
}

/** SuperAdmin / Manager see the full picker; Cashier and Waiter are assignment-scoped. */
export function seesEveryPosRegisterAssignment(
  user: PosAssignmentUser | null | undefined
): boolean {
  if (!user) return false;
  return hasNamedRole(user, 'SuperAdmin') || hasNamedRole(user, 'Manager');
}

export function isSelectableRegisterVisibleToUser(
  userId: string | undefined,
  assignedUserId: string | null | undefined
): boolean {
  if (assignedUserId == null || assignedUserId === '') return true;
  if (!userId) return false;
  return assignedUserId === userId;
}

export function filterSelectableRegistersForPosUser<
  T extends { assignedUserId?: string | null },
>(rows: T[], user: PosAssignmentUser | null | undefined): T[] {
  if (!user) return [];
  if (seesEveryPosRegisterAssignment(user)) return rows;
  return rows.filter((r) => isSelectableRegisterVisibleToUser(user.id, r.assignedUserId));
}
