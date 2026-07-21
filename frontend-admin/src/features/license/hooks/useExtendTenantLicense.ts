'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { AxiosError } from 'axios';

import {
  type ExtendTenantLicenseResult,
  extendTenantLicense,
} from '@/features/license/api/tenantLicense';
import { invalidateTenantLicenseQueries } from '@/features/license/utils/invalidateTenantLicenseQueries';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

export type ExtendTenantLicenseFormValues = {
  licenseKey: string;
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
  const { message } = useAntdApp();
  const { t } = useI18n();
  const queryClient = useQueryClient();

  const invalidate = () => {
    void invalidateTenantLicenseQueries(queryClient, tenantId);
  };

  return useMutation<ExtendTenantLicenseResult, unknown, ExtendTenantLicenseFormValues>({
    mutationFn: (values) =>
      extendTenantLicense({
        licenseKey: values.licenseKey.trim(),
      }),
    onSuccess: () => {
      invalidate();
    },
    onError: (error) => message.error(resolveExtendErrorMessage(error, t)),
  });
}
