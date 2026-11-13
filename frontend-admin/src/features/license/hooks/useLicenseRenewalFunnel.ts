'use client';

import { useQuery } from '@tanstack/react-query';

import {
  type LicenseRenewalFunnelDto,
  type LicenseRenewalFunnelParams,
  getLicenseRenewalFunnel,
  licenseQueryKeys,
} from '@/api/manual/adminLicense';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';

export function useLicenseRenewalFunnel(
  params: LicenseRenewalFunnelParams = {},
  enabled = true
) {
  const canAccess = useBillingAccess();

  return useQuery({
    queryKey: licenseQueryKeys.renewalFunnel(params),
    queryFn: () => getLicenseRenewalFunnel(params),
    enabled: enabled && canAccess,
  });
}

export type { LicenseRenewalFunnelDto, LicenseRenewalFunnelParams };
