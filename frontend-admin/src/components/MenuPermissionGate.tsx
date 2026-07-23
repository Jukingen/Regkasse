'use client';

import type { ReactNode } from 'react';

import { useMenuPermissions } from '@/hooks/useMenuPermissions';
import type { MenuAreaKey } from '@/shared/auth/menuPermissionRegistry';

/** Renders children only when the menu area is visible for the current user. */
export function MenuPermissionGate({
  menuKey,
  children,
}: {
  menuKey: MenuAreaKey | string;
  children: ReactNode;
}) {
  const { visible } = useMenuPermissions(menuKey);
  if (!visible) return null;
  return children;
}
