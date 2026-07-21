'use client';

import type { AdminTenantDetail } from '@/features/super-admin/api/adminTenants';
import { LicenseManager } from '@/features/super-admin/components/LicenseManager';

export type TenantDetailLicenseTabProps = {
  tenant: AdminTenantDetail;
  onUpdated: () => void;
};

export function TenantDetailLicenseTab(props: TenantDetailLicenseTabProps) {
  return <LicenseManager {...props} />;
}
