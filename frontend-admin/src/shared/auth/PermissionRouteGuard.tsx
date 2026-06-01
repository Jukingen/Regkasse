'use client';

import React, { ReactNode, useEffect, useMemo } from 'react';
import { usePathname, useRouter } from 'next/navigation';
import { useAuth, AuthStatus } from '@/features/auth/hooks/useAuth';
import { getRequiredPermissionForPath } from './routePermissions';
import { ALLOW_EMPTY_PERMISSIONS_FOR_ROUTE_ACCESS } from './routeGuardConfig';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { Spin } from 'antd';

interface PermissionRouteGuardProps {
  children: ReactNode;
}

type GuardState = 'loading' | 'unauthenticated' | 'no_permissions' | 'insufficient' | 'allowed';

function checkRoutePermission(pathname: string, permissions: string[]): boolean {
  const required = getRequiredPermissionForPath(pathname);
  if (required === undefined) return false;
  const arr = Array.isArray(required) ? required : [required];
  if (arr.length === 0) return permissions.length > 0;
  if (!permissions.length) return false;
  return arr.some((p) => permissions.includes(p));
}

/**
 * Protects routes by permission. Fail-closed by default:
 * - No permissions in token → redirect to /403 (unless migration flag is set).
 * - Insufficient permission for route → redirect to /403.
 * States: loading → spinner; unauthenticated → null (AuthGate redirects); no_permissions/insufficient → 403; allowed → children.
 */
export function PermissionRouteGuard({ children }: PermissionRouteGuardProps) {
  const pathname = usePathname();
  const router = useRouter();
  const { user, authStatus, isAuthInitializing } = useAuth();
  const permissions = (user as { permissions?: string[] } | undefined)?.permissions ?? [];

  const isProduction = process.env.NODE_ENV === 'production';
  // Production: ignore the migration env entirely (always false), even if a client bundle were misconfigured.
  const allowEmptyPermissionsForRouteAccess =
    !isProduction && ALLOW_EMPTY_PERMISSIONS_FOR_ROUTE_ACCESS;

  const state = useMemo((): GuardState => {
    if (isAuthInitializing) return 'loading';
    if (authStatus === AuthStatus.Unauthenticated) return 'unauthenticated';
    if (permissions.length === 0) {
      if (allowEmptyPermissionsForRouteAccess) return 'allowed';
      return 'no_permissions';
    }
    if (!checkRoutePermission(pathname, permissions)) return 'insufficient';
    return 'allowed';
  }, [isAuthInitializing, authStatus, permissions, pathname, allowEmptyPermissionsForRouteAccess]);

  useEffect(() => {
    if (process.env.NODE_ENV !== 'production') {
      technicalConsole.devLog(
        `[PermissionRouteGuard] path=${pathname} state=${state} permissionCount=${permissions.length} authStatus=${authStatus} initializing=${isAuthInitializing}`,
      );
    }
  }, [pathname, state, permissions.length, authStatus, isAuthInitializing]);

  useEffect(() => {
    if (state !== 'allowed' && state !== 'loading' && state !== 'unauthenticated') {
      router.replace('/403');
    }
  }, [state, router]);

  if (state === 'loading') {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 80 }}>
        <Spin size="large" description="Checking access..." />
      </div>
    );
  }

  if (state === 'unauthenticated') return null;

  if (state === 'no_permissions' || state === 'insufficient') return null;

  return <>{children}</>;
}
