import { AXIOS_INSTANCE } from '@/lib/axios';
import { authStorage } from '@/features/auth/services/authStorage';

export type AdminTenantListItem = {
    id: string;
    name: string;
    slug: string;
    email?: string | null;
    phone?: string | null;
    status: string;
    isActive: boolean;
    licenseKey?: string | null;
    licenseValidUntilUtc?: string | null;
    createdAt: string;
    updatedAt?: string | null;
};

export type AdminTenantDetail = AdminTenantListItem & {
    address?: string | null;
    deletedAtUtc?: string | null;
};

export type CreateAdminTenantRequest = {
    name: string;
    slug: string;
    email?: string | null;
    phone?: string | null;
    address?: string | null;
    licenseKey?: string | null;
    licenseValidUntilUtc?: string | null;
};

export type UpdateAdminTenantRequest = {
    name?: string | null;
    email?: string | null;
    phone?: string | null;
    address?: string | null;
    status?: string | null;
    licenseKey?: string | null;
    licenseValidUntilUtc?: string | null;
    isActive?: boolean | null;
};

export type TenantImpersonationResponse = {
    token: string;
    expiresIn: number;
    refreshToken?: string | null;
    refreshTokenExpiresAtUtc?: string | null;
    tenantId: string;
    tenantSlug: string;
    tenantDisplayName?: string | null;
    impersonation: boolean;
};

export async function listAdminTenants(includeDeleted = false): Promise<AdminTenantListItem[]> {
    const { data } = await AXIOS_INSTANCE.get<AdminTenantListItem[]>('/api/admin/tenants', {
        params: { includeDeleted },
    });
    return data;
}

export async function createAdminTenant(body: CreateAdminTenantRequest): Promise<AdminTenantDetail> {
    const { data } = await AXIOS_INSTANCE.post<AdminTenantDetail>('/api/admin/tenants', body);
    return data;
}

export async function updateAdminTenant(
    tenantId: string,
    body: UpdateAdminTenantRequest,
): Promise<AdminTenantDetail> {
    const { data } = await AXIOS_INSTANCE.put<AdminTenantDetail>(`/api/admin/tenants/${tenantId}`, body);
    return data;
}

export async function deleteAdminTenant(tenantId: string): Promise<void> {
    await AXIOS_INSTANCE.delete(`/api/admin/tenants/${tenantId}`);
}

export async function impersonateAdminTenant(tenantId: string): Promise<TenantImpersonationResponse> {
    const { data } = await AXIOS_INSTANCE.post<TenantImpersonationResponse>(
        `/api/admin/tenants/${tenantId}/impersonate`,
    );
    return data;
}

/** Applies impersonation token and reloads admin session. */
export function applyTenantImpersonationSession(res: TenantImpersonationResponse): void {
    authStorage.setToken(res.token);
    if (res.refreshToken) {
        authStorage.setRefreshToken(res.refreshToken);
    }
    if (typeof window !== 'undefined') {
        window.localStorage.setItem('dev_tenant_id', res.tenantSlug);
        window.location.reload();
    }
}
