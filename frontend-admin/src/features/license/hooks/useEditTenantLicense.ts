'use client';

import { useMutation } from '@tanstack/react-query';

import {
  type TenantLicenseOverview,
  type UpdateTenantLicenseRequest,
  putTenantLicense,
} from '@/features/license/api/tenantLicense';
import { useInvalidateTenantLicenseOverview } from '@/features/license/hooks/useTenantLicenseOverview';
import { useNotify } from '@/hooks/useNotify';

export type EditTenantLicenseVariables = {
  tenantId: string;
  body: UpdateTenantLicenseRequest;
};

export function useEditTenantLicense(options?: { onSuccess?: () => void }) {
  const notify = useNotify();
  const invalidateOverview = useInvalidateTenantLicenseOverview();

  return useMutation<TenantLicenseOverview, unknown, EditTenantLicenseVariables>({
    mutationFn: ({ tenantId, body }) => putTenantLicense(tenantId, body),
    onSuccess: () => {
      notify.successKey('license.superAdmin.editModal.success');
      invalidateOverview();
      options?.onSuccess?.();
    },
    onError: (error) => {
      notify.apiError(error, {
        logContext: 'License.editTenant',
        fallbackKey: 'license.superAdmin.editModal.error',
      });
    },
  });
}
