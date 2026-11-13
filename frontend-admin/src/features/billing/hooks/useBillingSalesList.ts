'use client';

import { useQuery } from '@tanstack/react-query';

import { getApiAdminBillingLicenseSales } from '@/api/generated/admin/admin';
import type { GetApiAdminBillingLicenseSalesParams } from '@/api/generated/model';
import { billingQueryKeys } from '@/features/billing/constants/billingQueryKeys';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';
import {
  type BillingSalesFilterState,
  toBillingSalesListApiParams,
} from '@/features/billing/utils/billingSalesFilters';

export type BillingSalesListFilters = BillingSalesFilterState;

export function useBillingSalesList(filters: BillingSalesFilterState) {
  const canAccess = useBillingAccess();
  const params = toBillingSalesListApiParams(filters) as GetApiAdminBillingLicenseSalesParams & {
    page: number;
    pageSize: number;
    licensePlan?: string;
    licenseType?: string;
    minDurationDays?: number;
    sortBy?: string;
    sortDir?: string;
  };

  return useQuery({
    queryKey: billingQueryKeys.salesList(params),
    queryFn: () => getApiAdminBillingLicenseSales(params),
    enabled: canAccess,
    staleTime: 30 * 1000,
    placeholderData: (previous) => previous,
  });
}
