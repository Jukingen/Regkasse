'use client';

import { usePathname } from 'next/navigation';

import { useSuperAdminTenantMode } from '@/features/tenancy/hooks/useSuperAdminTenantMode';
import { normalizeAdminPathname } from '@/shared/adminSidebarNavigation';
import { TenantHeader } from '@/shared/components/TenantHeader';

function AdminSectionTenantCard() {
  const pathname = usePathname();
  const { requiresTenantSelection } = useSuperAdminTenantMode();
  if (requiresTenantSelection) {
    return null;
  }
  // `/admin` page renders its own header above the tenant selector / redirect spinner.
  if (normalizeAdminPathname(pathname) === '/admin') {
    return null;
  }
  return <TenantHeader />;
}

export default function AdminSectionLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <AdminSectionTenantCard />
      {children}
    </>
  );
}
