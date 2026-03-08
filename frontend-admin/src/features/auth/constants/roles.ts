/**
 * Kanonik roller ve yetki yardımcıları – backend UsersView/UsersManage ile uyumlu.
 * Tek kaynak: menü, sayfa guard'ları ve buton görünürlüğü buradan türetilir.
 */

/** Backend UsersView policy: list/roles/search */
export const ROLES_CAN_VIEW_USERS = [
  'SuperAdmin',
  'Admin',
  'Administrator',
  'BranchManager',
  'Auditor',
] as const;

/** Backend UsersManage policy: create/update/deactivate/reactivate/reset-password */
export const ROLES_CAN_MANAGE_USERS = [
  'SuperAdmin',
  'Admin',
  'Administrator',
  'BranchManager',
] as const;

/** Sadece SuperAdmin rol oluşturabilir (POST /api/UserManagement/roles) */
export const ROLES_CAN_CREATE_ROLE = ['SuperAdmin'] as const;

/** RKSV menü: SuperAdmin, Admin, Administrator */
export const ROLES_RKSV_MENU = ['SuperAdmin', 'Admin', 'Administrator'] as const;

export type UserRole = string;

export function canViewUsers(role: UserRole | undefined | null): boolean {
  return ROLES_CAN_VIEW_USERS.includes(role as (typeof ROLES_CAN_VIEW_USERS)[number]);
}

export function canManageUsers(role: UserRole | undefined | null): boolean {
  return ROLES_CAN_MANAGE_USERS.includes(role as (typeof ROLES_CAN_MANAGE_USERS)[number]);
}

export function canCreateRole(role: UserRole | undefined | null): boolean {
  return ROLES_CAN_CREATE_ROLE.includes(role as (typeof ROLES_CAN_CREATE_ROLE)[number]);
}

export function isSuperAdmin(role: UserRole | undefined | null): boolean {
  return role === 'SuperAdmin';
}

/** Edit user (role, name, email, …) – UsersManage */
export function canEditUser(role: UserRole | undefined | null): boolean {
  return canManageUsers(role);
}

/** Deactivate / reactivate – UsersManage */
export function canDeactivateReactivate(role: UserRole | undefined | null): boolean {
  return canManageUsers(role);
}

/**
 * Force-reset another user's password.
 * Backend: only SuperAdmin can reset a SuperAdmin target.
 */
export function canResetPassword(
  actorRole: UserRole | undefined | null,
  targetRole: UserRole | undefined | null
): boolean {
  if (!canManageUsers(actorRole)) return false;
  if (targetRole === 'SuperAdmin') return isSuperAdmin(actorRole);
  return true;
}

export function canShowRksvMenu(role: UserRole | undefined | null): boolean {
  return ROLES_RKSV_MENU.includes(role as (typeof ROLES_RKSV_MENU)[number]);
}
