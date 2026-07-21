'use client';

import type { ReactNode } from 'react';

import { QuickActions } from '@/components/QuickActions';
import { usePermissions } from '@/hooks/usePermissions';

export type AdminLayoutProps = {
  children: ReactNode;
};

/**
 * Content shell for protected admin pages — wraps route output and Manager quick actions.
 */
export function AdminLayout({ children }: AdminLayoutProps) {
  const { isManager } = usePermissions();

  return (
    <div className="admin-layout">
      {children}
      {isManager ? <QuickActions /> : null}
    </div>
  );
}
