'use client';

import { GracePeriodBanner } from '@/components/GracePeriodBanner';
import { LicenseLockdownBanner } from '@/components/LicenseLockdownBanner';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useLicenseStatus } from '@/hooks/useLicenseStatus';
import { isLicenseLockdownSidebarActive } from '@/shared/sidebarLicenseLockdown';

/**
 * Mandant license lifecycle banner (Grace / Locked / Archived) with renew + data-export CTAs.
 * Grace UI is {@link GracePeriodBanner}; Locked/Archived UI is {@link LicenseLockdownBanner}.
 */
export function LicenseStatusBanner() {
  const tenant = useCurrentTenant();
  const { status, isLoading } = useLicenseStatus();

  if (isLoading || !status) return null;
  if (tenant.suppressLicenseWarnings || !tenant.isRealTenantSlug) return null;
  if (status.state === 'Active') return null;

  if (isLicenseLockdownSidebarActive(status.state)) {
    return <LicenseLockdownBanner status={status} />;
  }

  if (status.state === 'Grace') {
    return <GracePeriodBanner />;
  }

  return null;
}
