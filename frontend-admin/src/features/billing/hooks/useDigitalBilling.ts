'use client';

import { useQuery } from '@tanstack/react-query';

import { fetchDigitalBillingDashboard } from '@/features/billing/api/digitalBillingApi';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';

export const DIGITAL_BILLING_QUERY_KEY = ['admin', 'billing', 'digital'] as const;

export function useDigitalBilling() {
  const canAccess = useBillingAccess();
  return useQuery({
    queryKey: DIGITAL_BILLING_QUERY_KEY,
    queryFn: fetchDigitalBillingDashboard,
    enabled: canAccess,
  });
}
