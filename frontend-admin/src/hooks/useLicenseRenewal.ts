'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useCallback, useState } from 'react';

import { activateUnifiedLicense } from '@/features/license/api/activateUnifiedLicense';
import { useLicensePreview } from '@/features/license/hooks/useLicensePreview';
import {
  applyActivatedLicenseToCache,
  beginActivatedLicenseOptimisticUpdate,
  rollbackActivatedLicenseOptimisticUpdate,
} from '@/features/license/utils/applyActivatedLicenseToCache';
import { invalidateTenantLicenseQueries } from '@/features/license/utils/invalidateTenantLicenseQueries';
import {
  redirectToLicensePayment,
  resolveLicensePaymentRedirectTarget,
} from '@/features/license/utils/licensePaymentRedirect';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useI18n } from '@/i18n';

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

type RenewMutationVariables = {
  licenseKey: string;
  expectedValidUntilUtc?: string | null;
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
    mutationFn: async (variables: RenewMutationVariables) =>
      activateUnifiedLicense(variables.licenseKey, resolvedTenantId || null),
    onMutate: async (variables) => {
      const expectedValidUntilUtc = variables.expectedValidUntilUtc?.trim();
      if (!expectedValidUntilUtc) {
        return undefined;
      }
      return beginActivatedLicenseOptimisticUpdate(queryClient, {
        tenantId: resolvedTenantId,
        validUntilUtc: expectedValidUntilUtc,
        licenseKey: variables.licenseKey,
      });
    },
    onError: (_error, _variables, context) => {
      rollbackActivatedLicenseOptimisticUpdate(queryClient, context);
    },
    onSuccess: async (result, variables) => {
      applyActivatedLicenseToCache(queryClient, {
        tenantId: resolvedTenantId,
        validUntilUtc: result.validUntilUtc || variables.expectedValidUntilUtc || null,
        licenseKey: result.licenseKey || variables.licenseKey,
        licenseType: result.status,
      });
      if (resolvedTenantId) {
        await invalidateTenantLicenseQueries(queryClient, resolvedTenantId);
      } else {
        await queryClient.invalidateQueries({ queryKey: ['license'], refetchType: 'all' });
      }
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
        const result = await renewMutation.mutateAsync({
          licenseKey,
          expectedValidUntilUtc: previewMutation.data?.validUntilUtc,
        });
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
    [previewMutation.data?.validUntilUtc, renewMutation, t]
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
