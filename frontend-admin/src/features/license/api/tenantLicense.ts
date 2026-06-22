import { AXIOS_INSTANCE } from '@/lib/axios';

export type TenantLicenseStatus = {
    kind: string;
    licenseKey?: string | null;
    validUntilUtc?: string | null;
    daysRemaining?: number | null;
    tier?: string | null;
    features: string[];
};

export type TenantLicenseHistoryItem = {
    issuedLicenseId?: string | null;
    eventType: string;
    atUtc: string;
    summary: string;
    licenseKey?: string | null;
};

export type TenantLicenseOverview = {
    status: TenantLicenseStatus;
    history: TenantLicenseHistoryItem[];
};

export type UpdateTenantLicenseRequest = {
    licenseKey?: string | null;
    validUntilUtc: string;
};

export const tenantLicenseQueryKeys = {
    root: ['admin', 'tenant-license'] as const,
    detail: (tenantId: string) => [...tenantLicenseQueryKeys.root, tenantId] as const,
};

/** GET /api/admin/tenants/{tenantId}/license */
export async function getTenantLicense(tenantId: string): Promise<TenantLicenseOverview> {
    const { data } = await AXIOS_INSTANCE.get<TenantLicenseOverview>(
        `/api/admin/tenants/${tenantId}/license`,
    );
    return data;
}

/** PUT /api/admin/tenants/{tenantId}/license */
export async function putTenantLicense(
    tenantId: string,
    body: UpdateTenantLicenseRequest,
): Promise<TenantLicenseOverview> {
    const { data } = await AXIOS_INSTANCE.put<TenantLicenseOverview>(
        `/api/admin/tenants/${tenantId}/license`,
        body,
    );
    return data;
}
