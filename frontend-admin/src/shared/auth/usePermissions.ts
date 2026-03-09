'use client';

import { useMemo } from 'react';
import { useAuth } from '@/features/auth/hooks/useAuth';
import {
  hasPermission as checkPermission,
  hasAnyPermission as checkAnyPermission,
  hasAllPermissions as checkAllPermissions,
  type UserWithPermissions,
} from './permissions';

export function usePermissions() {
  const { user } = useAuth();
  const userWithPerms: UserWithPermissions | null = useMemo(
    () =>
      user
        ? { permissions: (user as { permissions?: string[] }).permissions ?? [] }
        : null,
    [user]
  );

  return useMemo(
    () => ({
      user,
      hasPermission: (permission: string) => checkPermission(userWithPerms, permission),
      hasAnyPermission: (permissions: string[]) =>
        checkAnyPermission(userWithPerms, permissions),
      hasAllPermissions: (permissions: string[]) =>
        checkAllPermissions(userWithPerms, permissions),
    }),
    [user, userWithPerms]
  );
}
