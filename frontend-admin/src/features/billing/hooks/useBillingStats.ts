'use client';

import type { GetApiAdminBillingStatsParams } from '@/api/generated/model';
import { billingApi } from '@/features/billing/api/billingApi';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';

export function useBillingStats(params?: GetApiAdminBillingStatsParams) {
    const canAccess = useBillingAccess();
    return billingApi.useStats(params, { query: { enabled: canAccess } });
}
