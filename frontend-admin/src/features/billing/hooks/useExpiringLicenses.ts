'use client';

import { billingApi } from '@/features/billing/api/billingApi';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';

export function useExpiringLicenses(daysThreshold = 30) {
    const canAccess = useBillingAccess();
    return billingApi.useExpiring({ daysThreshold }, { query: { enabled: canAccess } });
}
