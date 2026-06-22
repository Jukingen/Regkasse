'use client';

import { useMutation } from '@tanstack/react-query';

import {
    previewTenantLicense,
    type PreviewTenantLicenseRequest,
    type TenantLicensePreviewResult,
} from '@/features/license/api/tenantLicense';

export function useLicensePreview() {
    return useMutation<TenantLicensePreviewResult, unknown, PreviewTenantLicenseRequest>({
        mutationFn: (body) =>
            previewTenantLicense({
                licenseKey: body.licenseKey.trim(),
            }),
    });
}
