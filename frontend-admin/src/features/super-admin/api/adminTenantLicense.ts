import { AXIOS_INSTANCE } from '@/lib/axios';
import type {
    TenantLicenseHistoryItem,
    TenantLicenseOverview,
    TenantLicenseStatus,
    UpdateTenantLicenseRequest,
} from '@/features/license/api/tenantLicense';

export type {
    TenantLicenseHistoryItem,
    TenantLicenseOverview,
    TenantLicenseStatus,
    UpdateTenantLicenseRequest,
} from '@/features/license/api/tenantLicense';
export {
    getTenantLicense as getAdminTenantLicense,
    putTenantLicense as putAdminTenantLicense,
    tenantLicenseQueryKeys,
} from '@/features/license/api/tenantLicense';

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

/** @deprecated Prefer {@link UpdateTenantLicenseRequest} for Manager PUT; optional fields for Super Admin POST extend. */
export type ExtendTenantLicenseRequest = {
    licenseKey?: string | null;
    validUntilUtc?: string | null;
};

export type SetTenantLicenseTierRequest = {
    tier: 'basic' | 'standard' | 'premium';
    validUntilUtc?: string | null;
};

export type RenewTenantLicenseRequest = {
    additionalMonths: number;
    paymentConfirmed: boolean;
};

export type LicenseRenewalResult = {
    success: boolean;
    newExpiryDate?: string | null;
    daysAdded?: number;
    daysDeducted?: number;
    message?: string;
};

/** GET /api/admin/license/mandant — effective-tenant overview (Manager self-service). */
export async function getMandantLicenseOverview(): Promise<TenantLicenseOverview> {
    const { data } = await AXIOS_INSTANCE.get<TenantLicenseOverview>('/api/admin/license/mandant');
    return data;
}

/** POST /api/admin/license/mandant/extend — extend effective tenant with REGK key. */
export async function extendMandantLicense(
    body: ExtendTenantLicenseRequest,
): Promise<TenantLicenseOverview> {
    const { data } = await AXIOS_INSTANCE.post<TenantLicenseOverview>(
        '/api/admin/license/mandant/extend',
        body,
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

/** POST /api/admin/license/mandant/renew — mandant SaaS renewal for effective tenant. */
export async function renewMandantLicense(
    body: RenewTenantLicenseRequest,
): Promise<LicenseRenewalResult> {
    const { data } = await AXIOS_INSTANCE.post<LicenseRenewalResult>('/api/admin/license/mandant/renew', body);
    return data;
}

/** POST /api/admin/license/renew — mandant SaaS renewal with grace-period deduction. */
export async function renewAdminTenantLicense(
    tenantId: string,
    body: RenewTenantLicenseRequest,
): Promise<LicenseRenewalResult> {
    const { data } = await AXIOS_INSTANCE.post<LicenseRenewalResult>('/api/admin/license/renew', {
        tenantId,
        additionalMonths: body.additionalMonths,
        paymentConfirmed: body.paymentConfirmed,
    });
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
