/** Mirrors backend `CashRegisterAssignment` (Services/CashRegisterAssignment.cs). */
export type CashRegisterAssignmentState = 'unassigned' | 'assignedToMe' | 'assignedToOther';

/**
 * Classifies `cash_registers.assigned_user_id` relative to the signed-in admin.
 * Comparison is ordinal, like the backend visibility rule.
 */
export function resolveAssignmentState(
  assignedUserId: string | null | undefined,
  currentUserId: string | null | undefined
): CashRegisterAssignmentState {
  const assigned = assignedUserId?.trim();
  if (!assigned) {
    return 'unassigned';
  }
  const current = currentUserId?.trim();
  return current && current === assigned ? 'assignedToMe' : 'assignedToOther';
}

export type AssignmentTagColor = 'default' | 'success' | 'processing';

export function assignmentTagColor(state: CashRegisterAssignmentState): AssignmentTagColor {
  switch (state) {
    case 'assignedToMe':
      return 'success';
    case 'assignedToOther':
      return 'processing';
    default:
      return 'default';
  }
}
