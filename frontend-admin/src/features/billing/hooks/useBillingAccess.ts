'use client';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

export function useBillingAccess(): boolean {
  const { user, isAuthInitializing } = useAuth();
  if (isAuthInitializing) {
    return false;
  }
  return isSuperAdmin(user?.role) || hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL);
}
