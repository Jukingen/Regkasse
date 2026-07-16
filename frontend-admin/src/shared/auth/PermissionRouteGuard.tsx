'use client';

import React, { ReactNode, useEffect, useMemo } from 'react';
import { usePathname } from 'next/navigation';
import { useAuth, AuthStatus } from '@/features/auth/hooks/useAuth';
import { getRequiredPermissionForPath, permissionsSatisfyRoute } from './routePermissions';
import { ALLOW_EMPTY_PERMISSIONS_FOR_ROUTE_ACCESS } from './routeGuardConfig';
import { isChangePasswordPath } from '@/features/auth/constants/changePasswordRoute';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { ForbiddenAccessView } from '@/shared/auth/ForbiddenAccessView';
import { rememberAllowedAdminPath } from '@/shared/auth/useSafeNavigateBack';
import { Spin } from 'antd';

interface PermissionRouteGuardProps {
  children: ReactNode;
}

type GuardState = 'loading' | 'unauthenticated' | 'no_permissions' | 'insufficient' | 'allowed';

function checkRoutePermission(pathname: string, permissions: string[]): boolean {
  const required = getRequiredPermissionForPath(pathname);
  if (required === undefined) return false;
  return permissionsSatisfyRoute(pathname, permissions, required);
}

/**
 * Protects route content by permission. Fail-closed by default:
 * - No permissions in token → inline 403 (unless migration flag is set).
 * - Insufficient permission for route → inline 403.
 * Render inside the admin content area so sidebar/header stay visible.
 */
export function PermissionRouteGuard({ children }: PermissionRouteGuardProps) {
  const pathname = usePathname();
  const { user, authStatus, isAuthInitializing } = useAuth();
  const permissions = (user as { permissions?: string[] } | undefined)?.permissions ?? [];

  const isProduction = process.env.NODE_ENV === 'production';
  // Production: ignore the migration env entirely (always false), even if a client bundle were misconfigured.
  const allowEmptyPermissionsForRouteAccess =
    !isProduction && ALLOW_EMPTY_PERMISSIONS_FOR_ROUTE_ACCESS;

  const state = useMemo((): GuardState => {
    if (isAuthInitializing) return 'loading';
    if (authStatus === AuthStatus.Unauthenticated) return 'unauthenticated';

    const mustChangePassword = user?.mustChangePasswordOnNextLogin === true;
    if (mustChangePassword) {
      return isChangePasswordPath(pathname) ? 'allowed' : 'loading';
    }

    if (permissions.length === 0) {
      if (allowEmptyPermissionsForRouteAccess) return 'allowed';
      return 'no_permissions';
    }
    if (!checkRoutePermission(pathname, permissions)) return 'insufficient';
    return 'allowed';
  }, [isAuthInitializing, authStatus, permissions, pathname, allowEmptyPermissionsForRouteAccess, user?.mustChangePasswordOnNextLogin]);

  useEffect(() => {
    if (process.env.NODE_ENV !== 'production') {
      technicalConsole.devLog(
        `[PermissionRouteGuard] path=${pathname} state=${state} permissionCount=${permissions.length} authStatus=${authStatus} initializing=${isAuthInitializing}`,
      );
    }
  }, [pathname, state, permissions.length, authStatus, isAuthInitializing]);

  useEffect(() => {
    if (state === 'allowed') {
      rememberAllowedAdminPath(pathname);
    }
  }, [state, pathname]);

  if (state === 'loading') {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 80 }}>
        <Spin size="large" description="Checking access..." />
      </div>
    );
  }

  if (state === 'unauthenticated') return null;

  if (state === 'no_permissions' || state === 'insufficient') {
    return <ForbiddenAccessView compact />;
  }

  return <>{children}</>;
}
