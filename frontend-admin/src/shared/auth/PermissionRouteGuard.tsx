'use client';

import { usePathname, useRouter } from 'next/navigation';
import { useAuth, AuthStatus } from '@/features/auth/hooks/useAuth';
import { ROUTE_PERMISSIONS } from './routePermissions';
import { ReactNode, useEffect } from 'react';
import { Spin } from 'antd';

interface PermissionRouteGuardProps {
  children: ReactNode;
}

function checkRoutePermission(
  pathname: string,
  permissions: string[] | undefined
): boolean {
  const required = ROUTE_PERMISSIONS[pathname];
  if (required === undefined || (Array.isArray(required) && required.length === 0))
    return true;
  if (!permissions?.length) return false;
  const arr = Array.isArray(required) ? required : [required];
  return arr.some((p) => permissions.includes(p));
}

/**
 * Protects routes by permission. Redirects to /403 if user lacks required permission.
 * Use inside AuthGate so user is already authenticated. If backend does not send
 * permissions yet, allow access (fallback during migration).
 */
export function PermissionRouteGuard({ children }: PermissionRouteGuardProps) {
  const pathname = usePathname();
  const router = useRouter();
  const { user, authStatus, isInitialized } = useAuth();
  const permissions = (user as { permissions?: string[] } | undefined)?.permissions;
  const allowed =
    !permissions || permissions.length === 0
      ? true
      : checkRoutePermission(pathname, permissions);

  useEffect(() => {
    if (!isInitialized || authStatus !== AuthStatus.Authenticated) return;
    if (!allowed) router.replace('/403');
  }, [isInitialized, authStatus, allowed, router]);

  if (!isInitialized || authStatus === AuthStatus.Loading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 80 }}>
        <Spin size="large" tip="Checking access..." />
      </div>
    );
  }

  if (!allowed) return null;
  return <>{children}</>;
}
