'use client';

import type { AxiosError } from 'axios';
import { useMutation } from '@tanstack/react-query';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import {
    putTenantLicense,
    type TenantLicenseOverview,
    type UpdateTenantLicenseRequest,
} from '@/features/license/api/tenantLicense';
import { useInvalidateTenantLicenseOverview } from '@/features/license/hooks/useTenantLicenseOverview';

export type EditTenantLicenseVariables = {
    tenantId: string;
    body: UpdateTenantLicenseRequest;
};

function readApiErrorMessage(error: unknown, fallback: string): string {
    const axiosError = error as AxiosError<{ message?: string }>;
    const msg = axiosError.response?.data?.message;
    return typeof msg === 'string' && msg.trim().length > 0 ? msg.trim() : fallback;
}

export function useEditTenantLicense(options?: { onSuccess?: () => void }) {
    const { message } = useAntdApp();
    const { t } = useI18n();
    const invalidateOverview = useInvalidateTenantLicenseOverview();

    return useMutation<TenantLicenseOverview, unknown, EditTenantLicenseVariables>({
        mutationFn: ({ tenantId, body }) => putTenantLicense(tenantId, body),
        onSuccess: () => {
            message.success(t('license.superAdmin.editModal.success'));
            invalidateOverview();
            options?.onSuccess?.();
        },
        onError: (error) => {
            message.error(readApiErrorMessage(error, t('license.superAdmin.editModal.error')));
        },
    });
}
