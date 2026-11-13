'use client';

import { Spin } from 'antd';
import React, { FC, ReactNode, useEffect } from 'react';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { AuthStatus, useAuth } from '@/features/auth/hooks/useAuth';
import { ForbiddenAccessView } from '@/shared/auth/ForbiddenAccessView';

import { hasAnyPermission } from './permissions';

const ADMIN_PERMISSIONS = ['user.manage', 'settings.manage'] as const;

interface AdminOnlyGateProps {
  children: ReactNode;
}

/**
 * Super-admin-style gate (user.manage or settings.manage). Prefer route-level `PermissionRouteGuard` for feature areas.
 * Renders inline forbidden view so the admin shell stays visible.
 */
export const AdminOnlyGate: FC<AdminOnlyGateProps> = ({ children }) => {
  const { user, authStatus, isAuthInitializing } = useAuth();
  const permissions = user?.permissions ?? [];
  const role = user?.role ?? '';
  const isAdmin =
    permissions.length > 0 ? hasAnyPermission(user, [...ADMIN_PERMISSIONS]) : isSuperAdmin(role);

  if (isAuthInitializing) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', padding: 80 }}>
        <Spin size="large" description="Checking authorization..." />
      </div>
    );
  }

  if (authStatus === AuthStatus.Unauthenticated) return null;
  if (!isAdmin) return <ForbiddenAccessView compact />;

  return <>{children}</>;
};
