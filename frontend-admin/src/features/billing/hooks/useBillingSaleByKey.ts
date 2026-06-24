'use client';

import { billingApi } from '@/features/billing/api/billingApi';
import { billingQueryKeys } from '@/features/billing/constants/billingQueryKeys';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';

export function useBillingSaleByKey(licenseKey: string | undefined) {
    const canAccess = useBillingAccess();
    const key = licenseKey?.trim() ?? '';

    return billingApi.useGetByKey(key, {
        query: {
            enabled: canAccess && key.length > 0,
            queryKey: billingQueryKeys.salesByKey(key),
        },
    });
}
