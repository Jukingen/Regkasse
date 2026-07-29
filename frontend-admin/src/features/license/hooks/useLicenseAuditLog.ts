'use client';

import { useQuery } from '@tanstack/react-query';

import {
  type LicenseAuditLogListResponse,
  type LicenseAuditLogQueryParams,
  getLicenseAuditLog,
  licenseQueryKeys,
} from '@/api/manual/adminLicense';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';

export function useLicenseAuditLog(params: LicenseAuditLogQueryParams = {}, enabled = true) {
  const canAccess = useBillingAccess();

  return useQuery({
    queryKey: licenseQueryKeys.auditLog(params),
    queryFn: () => getLicenseAuditLog(params),
    enabled: enabled && canAccess,
  });
}

export type { LicenseAuditLogListResponse, LicenseAuditLogQueryParams };
