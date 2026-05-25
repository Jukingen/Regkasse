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

export type TenantLicenseConsistency = {
    isConsistent: boolean;
    warnings: string[];
    tenantValidUntilUtc?: string | null;
    matchedIssuedLicenseId?: string | null;
    matchedLicenseKey?: string | null;
    issuedExpiryAtUtc?: string | null;
    canIssueDeploymentLicense: boolean;
};

export type TenantLicenseIssueDeploymentResult = {
    success: boolean;
    message?: string | null;
    licenseKey?: string | null;
    issuedLicenseId?: string | null;
    overview?: TenantLicenseOverview | null;
};

export type TenantLicenseReminderResult = {
    success: boolean;
    recipientEmail: string;
    message?: string | null;
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

export async function checkAdminTenantLicenseConsistency(
    tenantId: string,
): Promise<TenantLicenseConsistency> {
    const { data } = await AXIOS_INSTANCE.post<TenantLicenseConsistency>(
        `/api/admin/tenants/${tenantId}/license/sync`,
    );
    return data;
}

export async function issueAdminTenantDeploymentLicense(
    tenantId: string,
): Promise<TenantLicenseIssueDeploymentResult> {
    const { data } = await AXIOS_INSTANCE.post<TenantLicenseIssueDeploymentResult>(
        `/api/admin/tenants/${tenantId}/license/sync/issue`,
    );
    return data;
}

export async function sendAdminTenantLicenseReminder(
    tenantId: string,
): Promise<TenantLicenseReminderResult> {
    const { data } = await AXIOS_INSTANCE.post<TenantLicenseReminderResult>(
        `/api/admin/tenants/${tenantId}/license/reminder`,
    );
    return data;
}
