/**
 * Admin user management API — platform vs tenant separation.
 * @see GET /api/admin/users?type=platform|tenant
 * @see POST /api/admin/users/invite
 * @see DELETE /api/admin/tenants/{tenantId}/users/{userId}
 */
import { customInstance } from '@/lib/axios';
import { postApiAdminUsers } from '@/api/generated/admin/admin';
import type { AdminUserDto as GeneratedAdminUserDto, UserInfo } from '@/api/generated/model';

export type AdminUserDto = {
    id: string;
    userName?: string | null;
    email?: string | null;
    firstName?: string | null;
    lastName?: string | null;
    employeeNumber?: string | null;
    role?: string | null;
    taxNumber?: string | null;
    notes?: string | null;
    isActive: boolean;
    createdAt?: string;
    lastLoginAt?: string | null;
};

export type TenantUserRowDto = {
    userId: string;
    email: string;
    name: string;
    role: string;
    isOwner: boolean;
    isActive: boolean;
    tenantId: string;
    tenantSlug: string;
    tenantName: string;
    joinedAtUtc: string;
    lastLoginAt?: string | null;
};

export type ListAdminUsersParams = {
    type?: 'platform' | 'tenant';
    tenantId?: string;
    role?: string;
    isActive?: boolean;
};

export type InviteAdminUserRequest = {
    email: string;
    tenantId: string;
    role: string;
    isOwner?: boolean;
};

export type TenantUserInviteResult = {
    user: {
        userId: string;
        email: string;
        name: string;
        role: string;
        isOwner: boolean;
        joinedAtUtc: string;
    };
    userCreated: boolean;
    invitationEmailSent: boolean;
    emailDeliveryNote?: string | null;
    generatedPassword?: string | null;
    forcePasswordChangeOnNextLogin?: boolean;
    tenantPortalUrl?: string | null;
};

export type CreatePlatformUserRequest = {
    userName: string;
    password: string;
    email?: string;
    firstName: string;
    lastName: string;
    employeeNumber?: string;
    taxNumber?: string;
    notes?: string;
};

export const adminUsersQueryKeys = {
    platform: (isActive?: boolean) => ['admin', 'users', 'platform', isActive ?? 'all'] as const,
    tenant: (tenantId?: string, role?: string) =>
        ['admin', 'users', 'tenant', tenantId ?? 'all', role ?? 'all'] as const,
};

export async function listPlatformUsers(params?: Pick<ListAdminUsersParams, 'isActive'>): Promise<AdminUserDto[]> {
    return customInstance<AdminUserDto[]>({
        url: '/api/admin/users',
        method: 'GET',
        params: { type: 'platform', ...(params?.isActive != null ? { isActive: params.isActive } : {}) },
    });
}

export async function listTenantUsers(params?: ListAdminUsersParams): Promise<TenantUserRowDto[]> {
    return customInstance<TenantUserRowDto[]>({
        url: '/api/admin/users',
        method: 'GET',
        params: {
            type: 'tenant',
            ...(params?.tenantId ? { tenantId: params.tenantId } : {}),
            ...(params?.role ? { role: params.role } : {}),
            ...(params?.isActive != null ? { isActive: params.isActive } : {}),
        },
    });
}

export async function inviteAdminUser(body: InviteAdminUserRequest): Promise<TenantUserInviteResult> {
    return customInstance<TenantUserInviteResult>({
        url: '/api/admin/users/invite',
        method: 'POST',
        data: body,
    });
}

/** Removes membership only — does not delete the user account. */
export async function removeUserFromTenant(tenantId: string, userId: string): Promise<void> {
    await customInstance<void>({
        url: `/api/admin/tenants/${tenantId}/users/${userId}`,
        method: 'DELETE',
    });
}

function mapGeneratedAdminUser(dto: GeneratedAdminUserDto): AdminUserDto {
    if (!dto.id) {
        throw new Error('Admin user response missing id');
    }
    return {
        id: dto.id,
        userName: dto.userName,
        email: dto.email,
        firstName: dto.firstName,
        lastName: dto.lastName,
        employeeNumber: dto.employeeNumber,
        role: dto.role,
        taxNumber: dto.taxNumber,
        notes: dto.notes,
        isActive: dto.isActive ?? false,
        createdAt: dto.createdAt,
        lastLoginAt: dto.lastLoginAt,
    };
}

export async function createPlatformUser(body: CreatePlatformUserRequest): Promise<AdminUserDto> {
    const created = await postApiAdminUsers({ ...body, role: 'SuperAdmin' });
    return mapGeneratedAdminUser(created);
}

export function adminUserToUserInfo(dto: AdminUserDto): UserInfo {
    return {
        id: dto.id,
        userName: dto.userName ?? undefined,
        email: dto.email ?? undefined,
        firstName: dto.firstName ?? '',
        lastName: dto.lastName ?? '',
        employeeNumber: dto.employeeNumber ?? undefined,
        role: dto.role ?? undefined,
        taxNumber: dto.taxNumber ?? undefined,
        notes: dto.notes ?? undefined,
        isActive: dto.isActive,
        createdAt: dto.createdAt,
        lastLoginAt: dto.lastLoginAt ?? undefined,
    };
}

export function tenantRowToTenantUser(row: TenantUserRowDto) {
    return {
        userId: row.userId,
        email: row.email,
        name: row.name,
        role: row.role,
        isOwner: row.isOwner,
        joinedAtUtc: row.joinedAtUtc,
        tenantId: row.tenantId,
        tenantSlug: row.tenantSlug,
        tenantName: row.tenantName,
        isActive: row.isActive,
        lastLoginAt: row.lastLoginAt ?? undefined,
    };
}

export type TenantUserRow = ReturnType<typeof tenantRowToTenantUser>;
