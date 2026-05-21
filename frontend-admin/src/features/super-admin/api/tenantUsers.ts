import { AXIOS_INSTANCE } from '@/lib/axios';

export type TenantUser = {
    userId: string;
    email: string;
    name: string;
    role: string;
    isOwner: boolean;
    joinedAtUtc: string;
};

export type AssignTenantUserRequest = {
    userId: string;
    role: string;
    isOwner?: boolean;
};

export type CreateTenantUserRequest = {
    email: string;
    role: string;
    isOwner?: boolean;
};

export type CreateTenantUserResult = {
    userId: string;
    email: string;
    generatedPassword: string;
    forcePasswordChangeOnNextLogin: boolean;
    success: boolean;
    tenantPortalUrl?: string | null;
    role?: string | null;
};

/** @deprecated Use CreateTenantUserRequest */
export type InviteTenantUserRequest = CreateTenantUserRequest;

/** @deprecated Use CreateTenantUserResult */
export type TenantUserInviteResult = {
    user: TenantUser;
    userCreated: boolean;
    invitationEmailSent: boolean;
    emailDeliveryNote?: string | null;
    generatedPassword?: string | null;
    forcePasswordChangeOnNextLogin?: boolean;
    tenantPortalUrl?: string | null;
};

export const INVITE_TENANT_ROLES = ['Manager', 'Cashier', 'Accountant'] as const;

export async function listTenantUsers(tenantId: string): Promise<TenantUser[]> {
    const { data } = await AXIOS_INSTANCE.get<TenantUser[]>(`/api/admin/tenants/${tenantId}/users`);
    return data;
}

export async function createTenantUser(
    tenantId: string,
    body: CreateTenantUserRequest,
): Promise<CreateTenantUserResult> {
    const { data } = await AXIOS_INSTANCE.post<CreateTenantUserResult>(
        `/api/admin/tenants/${tenantId}/users`,
        body,
    );
    return data;
}

export async function assignTenantUser(
    tenantId: string,
    body: AssignTenantUserRequest,
): Promise<TenantUser> {
    const { data } = await AXIOS_INSTANCE.post<TenantUser>(
        `/api/admin/tenants/${tenantId}/users/assign`,
        body,
    );
    return data;
}

/** @deprecated Prefer createTenantUser (POST /users) */
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

/** @deprecated Use assignTenantUser */
export type AddTenantUserRequest = AssignTenantUserRequest;

/** @deprecated Use assignTenantUser */
export const addTenantUser = assignTenantUser;

export type UpdateTenantUserRequest = {
    role?: string;
    isOwner?: boolean;
};

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

export async function updateTenantUserRole(
    tenantId: string,
    userId: string,
    role: string,
): Promise<TenantUser> {
    const { data } = await AXIOS_INSTANCE.put<TenantUser>(
        `/api/admin/tenants/${tenantId}/users/${userId}/role`,
        { role },
    );
    return data;
}

export type ResetTenantUserPasswordRequest = {
    sendEmail?: boolean;
};

export type TenantUserPasswordResetResult = {
    userId: string;
    email: string;
    generatedPassword: string;
    deliveryNote: string;
    emailSent: boolean;
    smtpConfigured: boolean;
    forcePasswordChangeOnNextLogin: boolean;
};

export async function resetTenantUserPassword(
    tenantId: string,
    userId: string,
    body?: ResetTenantUserPasswordRequest,
): Promise<TenantUserPasswordResetResult> {
    const { data } = await AXIOS_INSTANCE.post<TenantUserPasswordResetResult>(
        `/api/admin/tenants/${tenantId}/users/${userId}/reset-password`,
        body ?? {},
    );
    return data;
}

export async function removeTenantUser(tenantId: string, userId: string): Promise<void> {
    await AXIOS_INSTANCE.delete(`/api/admin/tenants/${tenantId}/users/${userId}`);
}
