import { AXIOS_INSTANCE } from '@/lib/axios';

export type TenantUser = {
    userId: string;
    email: string;
    name: string;
    role: string;
    isOwner: boolean;
    joinedAtUtc: string;
};

export type AddTenantUserRequest = {
    userId: string;
    role: string;
    isOwner?: boolean;
};

export type UpdateTenantUserRequest = {
    role?: string;
    isOwner?: boolean;
};

export type InviteTenantUserRequest = {
    email: string;
    role: string;
    isOwner?: boolean;
};

export type TenantUserInviteResult = {
    user: TenantUser;
    userCreated: boolean;
    invitationEmailSent: boolean;
    emailDeliveryNote?: string | null;
    generatedPassword?: string | null;
};

export const INVITE_TENANT_ROLES = ['Manager', 'Cashier', 'Accountant'] as const;

export async function listTenantUsers(tenantId: string): Promise<TenantUser[]> {
    const { data } = await AXIOS_INSTANCE.get<TenantUser[]>(`/api/admin/tenants/${tenantId}/users`);
    return data;
}

export async function addTenantUser(tenantId: string, body: AddTenantUserRequest): Promise<TenantUser> {
    const { data } = await AXIOS_INSTANCE.post<TenantUser>(`/api/admin/tenants/${tenantId}/users`, body);
    return data;
}

export async function inviteTenantUser(
    tenantId: string,
    body: InviteTenantUserRequest,
): Promise<TenantUserInviteResult> {
    const { data } = await AXIOS_INSTANCE.post<TenantUserInviteResult>(
        `/api/admin/tenants/${tenantId}/users/invite`,
        body,
    );
    return data;
}

export async function updateTenantUser(
    tenantId: string,
    userId: string,
    body: UpdateTenantUserRequest,
): Promise<TenantUser> {
    const { data } = await AXIOS_INSTANCE.put<TenantUser>(
        `/api/admin/tenants/${tenantId}/users/${userId}`,
        body,
    );
    return data;
}

export async function removeTenantUser(tenantId: string, userId: string): Promise<void> {
    await AXIOS_INSTANCE.delete(`/api/admin/tenants/${tenantId}/users/${userId}`);
}
