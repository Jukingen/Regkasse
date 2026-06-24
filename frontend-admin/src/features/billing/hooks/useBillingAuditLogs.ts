'use client';

import type { BillingAuditLogsFilters } from '@/features/billing/api/billingApi';
import { billingApi } from '@/features/billing/api/billingApi';
import { billingQueryKeys } from '@/features/billing/constants/billingQueryKeys';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';

export function useBillingAuditLogs(filters: BillingAuditLogsFilters = {}) {
    const canAccess = useBillingAccess();

    return billingApi.useAudit(filters, {
        query: {
            enabled: canAccess,
            queryKey: billingQueryKeys.auditList(filters),
        },
    });
}
