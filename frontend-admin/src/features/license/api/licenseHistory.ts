import { AXIOS_INSTANCE } from '@/lib/axios';

import type { TenantLicenseHistoryItem } from '@/features/license/api/tenantLicense';

export type MandantLicenseHistoryResponse = {
    tenantId: string;
    items: TenantLicenseHistoryItem[];
};

export const licenseHistoryQueryKeys = {
    root: ['admin', 'license', 'history'] as const,
    detail: (tenantId: string) => [...licenseHistoryQueryKeys.root, tenantId] as const,
};

/** GET /api/admin/license/history?tenantId=… — merged issued + audit + billing events. */
export async function getMandantLicenseHistory(
    tenantId: string,
): Promise<MandantLicenseHistoryResponse> {
    const { data } = await AXIOS_INSTANCE.get<MandantLicenseHistoryResponse>(
        '/api/admin/license/history',
        { params: { tenantId } },
    );
    return data;
}
