'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { AxiosError } from 'axios';

import { activateUnifiedLicense } from '@/features/license/api/activateUnifiedLicense';
import type { ExtendTenantLicenseResult } from '@/features/license/api/tenantLicense';
import {
  applyActivatedLicenseToCache,
  beginActivatedLicenseOptimisticUpdate,
  rollbackActivatedLicenseOptimisticUpdate,
} from '@/features/license/utils/applyActivatedLicenseToCache';
import { invalidateTenantLicenseQueries } from '@/features/license/utils/invalidateTenantLicenseQueries';
import { useNotify } from '@/hooks/useNotify';
import type { OptimisticQuerySnapshot } from '@/lib/query/optimisticUpdateHelpers';
import { useI18n } from '@/i18n';

export type ExtendTenantLicenseFormValues = {
  licenseKey: string;
  expectedValidUntilUtc?: string | null;
};

function readApiErrorMessage(error: unknown, fallback: string): string {
  const axiosError = error as AxiosError<{ message?: string }>;
  const msg = axiosError.response?.data?.message;
  return typeof msg === 'string' && msg.trim().length > 0 ? msg.trim() : fallback;
}

function resolveExtendErrorMessage(error: unknown, t: (key: string) => string): string {
  const axiosError = error as AxiosError<{ message?: string }>;
  const msg = axiosError.response?.data?.message ?? '';
  const normalized = msg.toLowerCase();
  if (
    normalized.includes('invalid license key') ||
    normalized.includes('issued license key was not found') ||
    normalized.includes('regk-xxxxx-xxxxx-xxxxx')
  ) {
    return t('license.extendModal.previewError');
  }
  if (normalized.includes('has expired')) {
    return t('license.extendModal.previewErrorExpired');
  }
  if (normalized.includes('not valid for this tenant')) {
    return t('license.extendModal.previewErrorWrongTenant');
  }
  return readApiErrorMessage(error, t('license.extendModal.error'));
}

export function useExtendTenantLicense(tenantId: string) {
  const notify = useNotify();
  const { t } = useI18n();
  const queryClient = useQueryClient();

  return useMutation<
    ExtendTenantLicenseResult,
    unknown,
    ExtendTenantLicenseFormValues,
    OptimisticQuerySnapshot | undefined
  >({
    mutationFn: (values) => activateUnifiedLicense(values.licenseKey, tenantId),
    onMutate: async (values) => {
      const expectedValidUntilUtc = values.expectedValidUntilUtc?.trim();
      if (!expectedValidUntilUtc) {
        return undefined;
      }
      return beginActivatedLicenseOptimisticUpdate(queryClient, {
        tenantId,
        validUntilUtc: expectedValidUntilUtc,
        licenseKey: values.licenseKey,
      });
    },
    onError: (error, _values, context) => {
      rollbackActivatedLicenseOptimisticUpdate(queryClient, context);
      notify.error(resolveExtendErrorMessage(error, t));
    },
    onSuccess: async (result, values) => {
      applyActivatedLicenseToCache(queryClient, {
        tenantId,
        validUntilUtc: result.validUntilUtc || values.expectedValidUntilUtc || null,
        licenseKey: result.licenseKey || values.licenseKey,
        licenseType: result.status,
      });
      await invalidateTenantLicenseQueries(queryClient, tenantId);
    },
  });
}
