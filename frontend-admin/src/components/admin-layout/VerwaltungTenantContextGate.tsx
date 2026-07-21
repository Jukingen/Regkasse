'use client';

import { usePathname } from 'next/navigation';

import { TenantInfoCard } from '@/components/admin-layout/TenantInfoCard';
import { useSuperAdminTenantMode } from '@/features/tenancy/hooks/useSuperAdminTenantMode';
import { isVerwaltungAdminPath, normalizeAdminPathname } from '@/shared/adminSidebarNavigation';

/** Tenant info card on Verwaltung routes when a mandant context is active (not blocked by SuperAdminTenantGate). */
export function VerwaltungTenantContextGate() {
  const pathname = usePathname();
  const { requiresTenantSelection } = useSuperAdminTenantMode();

  const p = normalizeAdminPathname(pathname);
  if (!isVerwaltungAdminPath(pathname)) {
    return null;
  }
  if (p === '/admin' || p.startsWith('/admin/')) {
    return null;
  }
  if (requiresTenantSelection) {
    return null;
  }

  return <TenantInfoCard />;
}
