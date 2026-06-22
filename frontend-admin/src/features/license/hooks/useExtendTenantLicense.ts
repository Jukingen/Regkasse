'use client';

import type { AxiosError } from 'axios';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import {
    putTenantLicense,
    tenantLicenseQueryKeys,
    type TenantLicenseOverview,
} from '@/features/license/api/tenantLicense';
import { computeExtendedValidUntilUtc } from '@/features/license/utils/tenantLicenseExtend';

export type ExtendTenantLicenseFormValues = {
    licenseKey: string;
    extendDays: number;
};

function readApiErrorMessage(error: unknown, fallback: string): string {
    const axiosError = error as AxiosError<{ message?: string }>;
    const msg = axiosError.response?.data?.message;
    return typeof msg === 'string' && msg.trim().length > 0 ? msg.trim() : fallback;
}

export function useExtendTenantLicense(
    tenantId: string,
    currentValidUntilUtc?: string | null,
) {
    const { message } = useAntdApp();
    const { t } = useI18n();
    const queryClient = useQueryClient();

    const invalidate = () => {
        void queryClient.invalidateQueries({ queryKey: tenantLicenseQueryKeys.detail(tenantId) });
        void queryClient.invalidateQueries({ queryKey: ['tenant-license-status'] });
        void queryClient.invalidateQueries({ queryKey: ['api', 'admin', 'tenants'] });
    };

    return useMutation<TenantLicenseOverview, unknown, ExtendTenantLicenseFormValues>({
        mutationFn: (values) => {
            const validUntilUtc = computeExtendedValidUntilUtc(
                currentValidUntilUtc,
                values.extendDays,
            );
            return putTenantLicense(tenantId, {
                licenseKey: values.licenseKey.trim(),
                validUntilUtc,
            });
        },
        onSuccess: () => {
            message.success(t('license.extendModal.success'));
            invalidate();
        },
        onError: (error) =>
            message.error(readApiErrorMessage(error, t('license.extendModal.error'))),
    });
}
