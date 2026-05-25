import type { UserInfo } from '@/features/users/api/usersGateway';
import type { TenantUserRow } from '@/features/users/api/users';

export type UnifiedAdminUserType = 'Platform' | 'Tenant';

export type UnifiedAdminUserRow = {
    key: string;
    kind: 'platform' | 'tenant';
    userType: UnifiedAdminUserType;
    userId: string;
    name: string;
    email: string;
    role: string;
    isActive: boolean;
    isPending?: boolean;
    lastLoginAt?: string | null;
    twoFactorEnabled?: boolean;
    tenantId?: string;
    tenantSlug: string;
    tenantName: string;
    isOwner?: boolean;
    user: UserInfo;
    row?: TenantUserRow;
};
