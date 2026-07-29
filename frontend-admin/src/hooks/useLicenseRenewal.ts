'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useCallback, useState } from 'react';

import {
  type ExtendTenantLicenseResult,
  extendTenantLicense,
} from '@/features/license/api/tenantLicense';
import { useLicensePreview } from '@/features/license/hooks/useLicensePreview';
import {
  redirectToLicensePayment,
  resolveLicensePaymentRedirectTarget,
} from '@/features/license/utils/licensePaymentRedirect';
import { invalidateTenantLicenseQueries } from '@/features/license/utils/invalidateTenantLicenseQueries';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useI18n } from '@/i18n';
import { tenantLicenseUnifiedQueryKey } from '@/hooks/useTenantLicense';

export type LicenseRenewalError = {
  message: string;
  details?: string;
};

export type LicenseRenewalSuccess = {
  success: true;
  validUntilUtc?: string | null;
  licenseKey?: string | null;
  message?: string;
};

function readApiMessage(error: unknown, fallback: string): string {
  const err = error as { response?: { data?: { message?: string } }; message?: string };
  const msg = err.response?.data?.message ?? err.message;
  return typeof msg === 'string' && msg.trim().length > 0 ? msg.trim() : fallback;
}

/**
 * Mandant license renewal: preview + activate billing/REGK key, optional payment redirect.
 */
export function useLicenseRenewal(tenantId?: string) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const tenant = useCurrentTenant();
  const resolvedTenantId = tenantId ?? tenant.tenantId ?? '';
  const previewMutation = useLicensePreview();
  const [error, setError] = useState<LicenseRenewalError | null>(null);

  const renewMutation = useMutation({
    mutationFn: async (licenseKey: string) => {
      const result = await extendTenantLicense({ licenseKey: licenseKey.trim() });
      return result;
    },
    onSuccess: () => {
      if (resolvedTenantId) {
        invalidateTenantLicenseQueries(queryClient, resolvedTenantId);
      }
      void queryClient.invalidateQueries({ queryKey: tenantLicenseUnifiedQueryKey });
    },
  });

  const preview = useCallback(
    async (licenseKey: string) => {
      setError(null);
      try {
        const result = await previewMutation.mutateAsync({ licenseKey: licenseKey.trim() });
        if (!result.valid) {
          setError({
            message: result.errorMessage?.trim() || t('license.renewalFlow.previewInvalid'),
            details: result.errorCode ?? undefined,
          });
          return { success: false as const, preview: result };
        }
        return { success: true as const, preview: result };
      } catch (err) {
        setError({
          message: readApiMessage(err, t('license.renewalFlow.previewFailed')),
        });
        return { success: false as const, preview: null };
      }
    },
    [previewMutation, t]
  );

  const renew = useCallback(
    async (licenseKey: string): Promise<LicenseRenewalSuccess | { success: false }> => {
      setError(null);
      try {
        const result: ExtendTenantLicenseResult = await renewMutation.mutateAsync(licenseKey);
        if (!result.success) {
          setError({
            message: result.message?.trim() || t('license.renewalFlow.activateFailed'),
          });
          return { success: false };
        }
        return {
          success: true,
          validUntilUtc: result.validUntilUtc,
          licenseKey: result.licenseKey,
          message: result.message,
        };
      } catch (err) {
        setError({
          message: readApiMessage(err, t('license.renewalFlow.activateFailed')),
        });
        return { success: false };
      }
    },
    [renewMutation, t]
  );

  const clearError = useCallback(() => setError(null), []);

  const paymentTarget = resolveLicensePaymentRedirectTarget({
    isSuperAdmin: tenant.isSuperAdminUser,
  });

  const goToPayment = useCallback(
    (pushInternal: (href: string) => void) => {
      redirectToLicensePayment({
        isSuperAdmin: tenant.isSuperAdminUser,
        pushInternal,
      });
    },
    [tenant.isSuperAdminUser]
  );

  return {
    preview,
    renew,
    goToPayment,
    paymentTarget,
    clearError,
    error,
    isPreviewing: previewMutation.isPending,
    isRenewing: renewMutation.isPending,
    lastPreview: previewMutation.data ?? null,
  };
}
