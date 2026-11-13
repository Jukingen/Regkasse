'use client';

import type { ReactNode } from 'react';

import { TenantGuard } from '@/components/TenantGuard';

type SuperAdminTenantGateProps = {
  children: ReactNode;
};

/**
 * @deprecated Prefer {@link TenantGuard}. Kept as a thin alias for existing imports.
 * Blocks mandant-scoped pages until Super Admin selects a tenant (JWT rebind / soft override).
 */
export function SuperAdminTenantGate({ children }: SuperAdminTenantGateProps) {
  return <TenantGuard>{children}</TenantGuard>;
}
