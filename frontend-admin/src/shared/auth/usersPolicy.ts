/**
 * Users ekranı yetki matrisi – Backend UsersView/UsersManage ile tek kaynak.
 * UI görünürlüğü ve mutation guard buradan türetilir.
 *
 * Backend–FE eşleşme: ai/USERS_AUTH_MATRIX.md
 */
import { useMemo } from 'react';
import {
  canViewUsers,
  canManageUsers,
  canCreateRole as roleCanCreateRole,
  canResetPassword as roleCanResetPassword,
} from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';

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
  /** Hedef kullanıcının rolüne göre (SuperAdmin sadece SuperAdmin tarafından reset edilebilir). */
  canResetPassword: (targetRole: string | undefined | null) => boolean;
}

/**
 * Aktör rolüne göre Users ekranı aksiyon yetkilerini döndürür.
 * Tek kaynak: sayfa ve mutation guard bu objeyi kullanır.
 */
export function getUsersPolicy(actorRole: string | undefined | null): UsersPolicy {
  const role = actorRole ?? '';
  return {
    canView: canViewUsers(role),
    canCreate: canManageUsers(role),
    canEdit: canManageUsers(role),
    canDeactivate: canManageUsers(role),
    canReactivate: canManageUsers(role),
    canCreateRole: roleCanCreateRole(role),
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
 * Giriş yapmış kullanıcının Users ekranı yetkileri (sayfa/hook tek kaynak).
 */
export function useUsersPolicy(): UsersPolicy {
  const { user } = useAuth();
  return useMemo(() => getUsersPolicy(user?.role), [user?.role]);
}
