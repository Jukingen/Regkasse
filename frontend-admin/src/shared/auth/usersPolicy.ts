/**
 * Users screen policy – permission-first when user.permissions exist, else role-based fallback.
 * Backend: user.view (list), user.manage (create/edit/deactivate/reactivate/reset), role.create (SuperAdmin only).
 */
import { useMemo } from 'react';
import { PERMISSIONS } from './permissions';
import { hasPermission } from './permissions';
import { permissionImplied } from './permissionImplication';
import {
  canViewUsers,
  canManageUsers,
  canCreateRole as roleCanCreateRole,
  canDeleteRole as roleCanDeleteRole,
  canEditRolePermissions as roleCanEditRolePermissions,
  canResetPassword as roleCanResetPassword,
  canProvisionTenantCredentials,
  isManager,
  isSuperAdmin,
} from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import type { UserWithPermissions } from './permissions';

export type UsersAction =
  | 'view'
  | 'create'
  | 'edit'
  | 'deactivate'
  | 'reactivate'
  | 'resetPassword'
  | 'createRole'
  | 'deleteRole'
  | 'editRolePermissions';

export interface UsersPolicy {
  canView: boolean;
  canCreate: boolean;
  canEdit: boolean;
  canDeactivate: boolean;
  canReactivate: boolean;
  canCreateRole: boolean;
  canDeleteRole: boolean;
  canEditRolePermissions: boolean;
  canResetPassword: (targetRole: string | undefined | null) => boolean;
  /** Generated one-time password flow (Manager); false when manual SuperAdmin reset is used. */
  useGeneratedPasswordReset: boolean;
  /** Mandant user create with generated password — SuperAdmin only (not Manager). */
  canProvisionTenantCredentials: boolean;
  /** Individual permission overrides (user.manage). */
  canManagePermissions: boolean;
}

/**
 * Permission-first when JWT lists permissions; canonical Manager/SuperAdmin still grant user.view via role fallback.
 */
function resolveCanViewUsers(
  role: string | undefined | null,
  permissions?: string[],
): boolean {
  if (permissions?.length) {
    const holder: UserWithPermissions = { permissions };
    if (hasPermission(holder, PERMISSIONS.USER_VIEW)) return true;
    if (permissionImplied(PERMISSIONS.USER_VIEW, permissions)) return true;
  }
  return canViewUsers(role);
}

/**
 * Permission-first: when permissions array is present, use it; otherwise fall back to role.
 */
export function getUsersPolicy(
  actorRole: string | undefined | null,
  permissions?: string[]
): UsersPolicy {
  const role = actorRole ?? '';
  const usePerms = permissions && permissions.length > 0;
  const userWithPerms: UserWithPermissions | null = usePerms ? { permissions } : null;

  return {
    canView: resolveCanViewUsers(role, permissions),
    canCreate: usePerms ? hasPermission(userWithPerms, PERMISSIONS.USER_MANAGE) : canManageUsers(role),
    canEdit: usePerms ? hasPermission(userWithPerms, PERMISSIONS.USER_MANAGE) : canManageUsers(role),
    canDeactivate: usePerms ? hasPermission(userWithPerms, PERMISSIONS.USER_MANAGE) : canManageUsers(role),
    canReactivate: usePerms ? hasPermission(userWithPerms, PERMISSIONS.USER_MANAGE) : canManageUsers(role),
    canCreateRole: usePerms ? hasPermission(userWithPerms, PERMISSIONS.ROLE_MANAGE) : roleCanCreateRole(role),
    canDeleteRole: usePerms ? hasPermission(userWithPerms, PERMISSIONS.ROLE_MANAGE) : roleCanDeleteRole(role),
    canEditRolePermissions: usePerms ? hasPermission(userWithPerms, PERMISSIONS.ROLE_MANAGE) : roleCanEditRolePermissions(role),
    canResetPassword: (targetRole: string | undefined | null) => {
      const target = targetRole ?? '';
      if (usePerms) {
        const canReset =
          permissionImplied(PERMISSIONS.USER_RESET_PASSWORD, permissions!) ||
          permissionImplied(PERMISSIONS.USER_MANAGE, permissions!);
        if (!canReset) return false;
      } else if (!roleCanResetPassword(role, target)) {
        return false;
      }
      if (target === 'SuperAdmin' && !isSuperAdmin(role)) return false;
      return true;
    },
    useGeneratedPasswordReset: usePerms
      ? permissionImplied(PERMISSIONS.USER_RESET_PASSWORD, permissions!) &&
        !permissionImplied(PERMISSIONS.USER_MANAGE, permissions!)
      : isManager(role) && !isSuperAdmin(role),
    canProvisionTenantCredentials: canProvisionTenantCredentials(role),
    canManagePermissions: usePerms
      ? hasPermission(userWithPerms, PERMISSIONS.USER_MANAGE)
      : canManageUsers(role),
  };
}

/**
 * Tekil aksiyon kontrolü (programatik guard / test).
 * context: resetPassword için { targetRole } gerekir.
 */
export function canUsers(
  action: UsersAction,
  actorRole: string | undefined | null,
  context?: { targetRole?: string | null }
): boolean {
  const policy = getUsersPolicy(actorRole);
  switch (action) {
    case 'view':
      return policy.canView;
    case 'create':
      return policy.canCreate;
    case 'edit':
      return policy.canEdit;
    case 'deactivate':
      return policy.canDeactivate;
    case 'reactivate':
      return policy.canReactivate;
    case 'resetPassword':
      return policy.canResetPassword(context?.targetRole ?? '');
    case 'createRole':
      return policy.canCreateRole;
    case 'deleteRole':
      return policy.canDeleteRole;
    case 'editRolePermissions':
      return policy.canEditRolePermissions;
    default:
      return false;
  }
}

/**
 * Current user's Users screen policy – permission-first when backend sends permissions.
 */
export function useUsersPolicy(): UsersPolicy {
  const { user } = useAuth();
  const permissions = (user as { permissions?: string[] } | undefined)?.permissions;
  const actorRole = user?.role ?? user?.roles?.[0] ?? null;
  return useMemo(
    () => getUsersPolicy(actorRole, permissions),
    [actorRole, permissions],
  );
}
