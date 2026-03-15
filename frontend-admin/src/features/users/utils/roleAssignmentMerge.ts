/**
 * Role assignment merge helpers – catalog-driven UI, assigned-only for checked state.
 * Used by user-role assignment: fullRoleCatalog = source of truth for visible list;
 * assignedRoleIds = source of truth for checked state; checked = assignedRoleIds.includes(role.id || role.name).
 * Backend is single-role per user (UserInfo.role), so assignedRoleIds has at most one element.
 */

export interface UserWithRole {
  role?: string | null;
}

/**
 * Returns the list of role ids/names assigned to the user.
 * Single-role model: at most one element. Empty when user is null or has no role.
 */
export function getAssignedRoleIdsFromUser(user: UserWithRole | null | undefined): string[] {
  if (user == null) return [];
  const role = user.role;
  if (role == null || role === '') return [];
  return [role];
}

/**
 * Returns whether the given role (id or name) is in the assigned set (i.e. should be checked in UI).
 */
export function isRoleChecked(roleIdOrName: string, assignedRoleIds: string[]): boolean {
  if (!roleIdOrName) return false;
  return assignedRoleIds.includes(roleIdOrName);
}
