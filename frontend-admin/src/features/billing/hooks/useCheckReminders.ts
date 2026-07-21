'use client';

import { billingApi } from '@/features/billing/api/billingApi';

export function useCheckReminders() {
  return billingApi.useCheckReminders();
}
