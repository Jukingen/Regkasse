'use client';

import type { ActivateLicenseRequest } from '@/api/generated/model';
import { billingApi } from '@/features/billing/api/billingApi';

export function useActivateLicense() {
  return billingApi.useActivate();
}

export type { ActivateLicenseRequest };
