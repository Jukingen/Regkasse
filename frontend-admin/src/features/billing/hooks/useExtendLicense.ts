'use client';

import type { ExtendLicenseRequest } from '@/api/generated/model';
import { billingApi } from '@/features/billing/api/billingApi';

export function useExtendLicense() {
    return billingApi.useExtend();
}

export type { ExtendLicenseRequest };
