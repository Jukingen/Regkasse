'use client';

import { billingApi } from '@/features/billing/api/billingApi';

export function useSendReminders() {
    return billingApi.useSendReminders();
}
