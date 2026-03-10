/**
 * Canonical roles and role-based helpers – aligned with backend Roles.cs.
 * Admin role removed from backend; legacy JWT may still carry "Admin" until re-login – treat as SuperAdmin-equivalent where needed.
 * Prefer permission checks when user.permissions is available; use these for fallback/display only.
 */

/** Canonical role set – backend Roles.Canonical (no Admin; SuperAdmin is sole top admin). */
export const ROLES_CANONICAL = [
  'SuperAdmin',
  'Manager',
  'Cashier',
  'Waiter',
  'Kitchen',
  'ReportViewer',
  'Accountant',
] as const;

export type CanonicalRole = (typeof ROLES_CANONICAL)[number];

/**
 * Legacy role name merged to SuperAdmin on backend; still allow FE fallback until tokens refresh.
 */
const LEGACY_ADMIN_ROLE = 'Admin';

/** Roles that have user.view (list/roles/search). Backend: Manager, SuperAdmin; legacy Admin treated same as SuperAdmin. */
export const ROLES_CAN_VIEW_USERS = ['SuperAdmin', 'Manager'] as const;

/** Roles that have user.manage – backend after migration: SuperAdmin; legacy Admin included for backward compat. */
export const ROLES_CAN_MANAGE_USERS = ['SuperAdmin', LEGACY_ADMIN_ROLE] as const;

/** Sadece SuperAdmin rol oluşturabilir (POST /api/UserManagement/roles) */
export const ROLES_CAN_CREATE_ROLE = ['SuperAdmin'] as const;

/** Sadece SuperAdmin rol silebilir (DELETE /api/UserManagement/roles/{roleName}); custom roller. */
export const ROLES_CAN_DELETE_ROLE = ['SuperAdmin'] as const;

/** Sadece SuperAdmin rol izinlerini düzenleyebilir (PUT .../roles/{roleName}/permissions). */
export const ROLES_CAN_EDIT_ROLE_PERMISSIONS = ['SuperAdmin'] as const;

/** RKSV menü: SuperAdmin only in new model; legacy Admin still sees menu until re-login. */
export const ROLES_RKSV_MENU = ['SuperAdmin', LEGACY_ADMIN_ROLE] as const;

export type UserRole = string;

function normalizeRoleForPolicy(role: UserRole | undefined | null): string | null {
  if (role == null || role === '') return null;
  if (role === LEGACY_ADMIN_ROLE) return 'SuperAdmin';
  return role;
}

export function canViewUsers(role: UserRole | undefined | null): boolean {
  const r = normalizeRoleForPolicy(role);
  if (r === 'SuperAdmin') return true;
  return ROLES_CAN_VIEW_USERS.includes(r as (typeof ROLES_CAN_VIEW_USERS)[number]);
}

export function canManageUsers(role: UserRole | undefined | null): boolean {
  if (role === LEGACY_ADMIN_ROLE || role === 'SuperAdmin') return true;
  return ROLES_CAN_MANAGE_USERS.includes(role as (typeof ROLES_CAN_MANAGE_USERS)[number]);
}

export function canCreateRole(role: UserRole | undefined | null): boolean {
  return ROLES_CAN_CREATE_ROLE.includes(role as (typeof ROLES_CAN_CREATE_ROLE)[number]);
}

export function canDeleteRole(role: UserRole | undefined | null): boolean {
  return ROLES_CAN_DELETE_ROLE.includes(role as (typeof ROLES_CAN_DELETE_ROLE)[number]);
}

export function canEditRolePermissions(role: UserRole | undefined | null): boolean {
  return ROLES_CAN_EDIT_ROLE_PERMISSIONS.includes(role as (typeof ROLES_CAN_EDIT_ROLE_PERMISSIONS)[number]);
}

export function isSuperAdmin(role: UserRole | undefined | null): boolean {
  return role === 'SuperAdmin' || role === LEGACY_ADMIN_ROLE;
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
  if (targetRole === 'SuperAdmin' || targetRole === LEGACY_ADMIN_ROLE) return isSuperAdmin(actorRole);
  return true;
}

export function canShowRksvMenu(role: UserRole | undefined | null): boolean {
  return ROLES_RKSV_MENU.includes(role as (typeof ROLES_RKSV_MENU)[number]);
}
