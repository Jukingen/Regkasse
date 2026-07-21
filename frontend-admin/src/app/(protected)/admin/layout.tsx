'use client';

import { usePathname } from 'next/navigation';

import { TenantInfoCard } from '@/components/admin-layout/TenantInfoCard';
import { useSuperAdminTenantMode } from '@/features/tenancy/hooks/useSuperAdminTenantMode';
import { normalizeAdminPathname } from '@/shared/adminSidebarNavigation';

function AdminSectionTenantCard() {
  const pathname = usePathname();
  const { requiresTenantSelection } = useSuperAdminTenantMode();
  if (requiresTenantSelection) {
    return null;
  }
  // `/admin` page renders its own card above the tenant selector / redirect spinner.
  if (normalizeAdminPathname(pathname) === '/admin') {
    return null;
  }
  return <TenantInfoCard />;
}

export default function AdminSectionLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <AdminSectionTenantCard />
      {children}
    </>
  );
}
