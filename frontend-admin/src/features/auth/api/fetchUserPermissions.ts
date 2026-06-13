import type { QueryClient } from '@tanstack/react-query';
import { AUTH_KEYS, fetchAuthUser } from '@/features/auth/hooks/useAuth';
import { technicalConsole } from '@/shared/dev/technicalConsole';

/**
 * Loads effective permissions for the **current session user**.
 *
 * Primary source: GET `/api/Auth/me` (same effective set as JWT claims).
 * Optional admin alias: GET `/api/admin/users/{id}/permissions` (self or SuperAdmin) — same resolver output.
 * We intentionally do not call `/api/UserManagement/{id}/permissions/effective` here —
 * that endpoint requires `user.manage` and is for admin user-management screens only.
 */
export async function fetchUserPermissions(): Promise<string[]> {
  try {
    const user = await fetchAuthUser();
    return user.permissions ?? [];
  } catch (error) {
    technicalConsole.error('Failed to fetch user permissions from /me', error);
    return [];
  }
}

/** Invalidates and refetches `/me`, returning the latest permission list. */
export async function refreshUserPermissions(queryClient: QueryClient): Promise<string[]> {
  await queryClient.invalidateQueries({ queryKey: AUTH_KEYS.user });
  try {
    const user = await queryClient.fetchQuery({
      queryKey: AUTH_KEYS.user,
      queryFn: fetchAuthUser,
    });
    return user.permissions ?? [];
  } catch (error) {
    technicalConsole.error('Failed to refresh user permissions from /me', error);
    return [];
  }
}
