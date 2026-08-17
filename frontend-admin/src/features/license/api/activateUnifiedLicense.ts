import { postActivateLicense } from '@/api/manual/adminLicense';
import type { ExtendTenantLicenseResult } from '@/features/license/api/tenantLicense';

/** Activate a unified REGK key via POST /api/license/activate. */
export async function activateUnifiedLicense(
  licenseKey: string,
  tenantId?: string | null
): Promise<ExtendTenantLicenseResult> {
  const key = licenseKey.trim();
  const result = await postActivateLicense({
    licenseKey: key,
    tenantId: tenantId?.trim() ? tenantId.trim() : undefined,
  });
  if (!result.success) {
    const err = new Error(result.message?.trim() || 'License activation failed') as Error & {
      response?: { data?: { message?: string } };
    };
    err.response = { data: { message: result.message ?? undefined } };
    throw err;
  }

  return {
    success: true,
    licenseKey: key,
    validUntilUtc: result.validUntil ?? '',
    status: result.licenseType ?? 'Licensed',
    message: result.message ?? '',
  };
}
