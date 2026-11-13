'use client';

import { useMutation } from '@tanstack/react-query';

import {
  type PreviewTenantLicenseRequest,
  type TenantLicensePreviewResult,
  previewTenantLicense,
} from '@/features/license/api/tenantLicense';

/** Super Admin / Manager preview via POST /api/admin/license/mandant/preview (unified lookup). */
export function useLicensePreview() {
  return useMutation<TenantLicensePreviewResult, unknown, PreviewTenantLicenseRequest>({
    mutationFn: (body) =>
      previewTenantLicense({
        licenseKey: body.licenseKey.trim(),
      }),
  });
}
