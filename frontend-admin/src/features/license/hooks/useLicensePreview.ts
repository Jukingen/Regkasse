'use client';

import { useMutation } from '@tanstack/react-query';

import {
  type PreviewTenantLicenseRequest,
  type TenantLicensePreviewResult,
  previewTenantLicense,
} from '@/features/license/api/tenantLicense';

export function useLicensePreview() {
  return useMutation<TenantLicensePreviewResult, unknown, PreviewTenantLicenseRequest>({
    mutationFn: (body) =>
      previewTenantLicense({
        licenseKey: body.licenseKey.trim(),
      }),
  });
}
