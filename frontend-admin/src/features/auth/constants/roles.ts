/**
 * Canonical roles and role-based helpers – aligned with backend Roles.cs.
 * Prefer permission checks when user.permissions is available; use these for fallback/display only.
 */

/** Canonical role set – backend Roles.Canonical (SuperAdmin, Admin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant). */
export const ROLES_CANONICAL = [
  'SuperAdmin',
  'Admin',
  'Manager',
  'Cashier',
  'Waiter',
  'Kitchen',
  'ReportViewer',
  'Accountant',
] as const;

export type CanonicalRole = (typeof ROLES_CANONICAL)[number];

/** Roles that have user.view (list/roles/search). Backend: Manager, Admin, SuperAdmin. */
export const ROLES_CAN_VIEW_USERS = [
  'SuperAdmin',
  'Admin',
  'Manager',
] as const;

/** Roles that have user.manage (create/update/deactivate/reactivate/reset-password). Backend: Admin, SuperAdmin only. */
export const ROLES_CAN_MANAGE_USERS = [
  'SuperAdmin',
  'Admin',
] as const;

/** Sadece SuperAdmin rol oluşturabilir (POST /api/UserManagement/roles) */
export const ROLES_CAN_CREATE_ROLE = ['SuperAdmin'] as const;

/** RKSV menü: SuperAdmin, Admin */
export const ROLES_RKSV_MENU = ['SuperAdmin', 'Admin'] as const;

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
