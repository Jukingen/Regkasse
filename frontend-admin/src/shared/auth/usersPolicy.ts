/**
 * Users screen policy – permission-first when user.permissions exist, else role-based fallback.
 * Backend: user.view (list), user.manage (create/edit/deactivate/reactivate/reset), role.create (SuperAdmin only).
 */
import { useMemo } from 'react';
import { PERMISSIONS } from './permissions';
import { hasPermission } from './permissions';
import {
  canViewUsers,
  canManageUsers,
  canCreateRole as roleCanCreateRole,
  canResetPassword as roleCanResetPassword,
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
  | 'createRole';

export interface UsersPolicy {
  canView: boolean;
  canCreate: boolean;
  canEdit: boolean;
  canDeactivate: boolean;
  canReactivate: boolean;
  canCreateRole: boolean;
  canResetPassword: (targetRole: string | undefined | null) => boolean;
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
    canView: usePerms ? hasPermission(userWithPerms, PERMISSIONS.USER_VIEW) : canViewUsers(role),
    canCreate: usePerms ? hasPermission(userWithPerms, PERMISSIONS.USER_MANAGE) : canManageUsers(role),
    canEdit: usePerms ? hasPermission(userWithPerms, PERMISSIONS.USER_MANAGE) : canManageUsers(role),
    canDeactivate: usePerms ? hasPermission(userWithPerms, PERMISSIONS.USER_MANAGE) : canManageUsers(role),
    canReactivate: usePerms ? hasPermission(userWithPerms, PERMISSIONS.USER_MANAGE) : canManageUsers(role),
    canCreateRole: usePerms ? hasPermission(userWithPerms, PERMISSIONS.ROLE_MANAGE) : roleCanCreateRole(role),
    canResetPassword: (targetRole: string | undefined | null) =>
      roleCanResetPassword(role, targetRole ?? ''),
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
  return useMemo(
    () => getUsersPolicy(user?.role ?? null, permissions),
    [user?.role, permissions]
  );
}
