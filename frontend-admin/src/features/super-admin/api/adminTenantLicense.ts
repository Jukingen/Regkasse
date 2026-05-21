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

export type ExtendTenantLicenseRequest = {
    licenseKey?: string | null;
    validUntilUtc?: string | null;
};

export type SetTenantLicenseTierRequest = {
    tier: 'basic' | 'standard' | 'premium';
    validUntilUtc?: string | null;
};

export async function getAdminTenantLicense(tenantId: string): Promise<TenantLicenseOverview> {
    const { data } = await AXIOS_INSTANCE.get<TenantLicenseOverview>(
        `/api/admin/tenants/${tenantId}/license`,
    );
    return data;
}

export async function activateAdminTenantTrial(tenantId: string): Promise<TenantLicenseOverview> {
    const { data } = await AXIOS_INSTANCE.post<TenantLicenseOverview>(
        `/api/admin/tenants/${tenantId}/license/trial`,
    );
    return data;
}

export async function extendAdminTenantLicense(
    tenantId: string,
    body: ExtendTenantLicenseRequest,
): Promise<TenantLicenseOverview> {
    const { data } = await AXIOS_INSTANCE.post<TenantLicenseOverview>(
        `/api/admin/tenants/${tenantId}/license/extend`,
        body,
    );
    return data;
}

export async function setAdminTenantLicenseTier(
    tenantId: string,
    body: SetTenantLicenseTierRequest,
): Promise<TenantLicenseOverview> {
    const { data } = await AXIOS_INSTANCE.post<TenantLicenseOverview>(
        `/api/admin/tenants/${tenantId}/license/tier`,
        body,
    );
    return data;
}
