import type { UserInfo } from '@/features/users/api/usersGateway';
import type { TenantUserRow } from '@/features/users/api/users';

export type UnifiedAdminUserRow =
    | {
          kind: 'platform';
          key: string;
          tenantSlug: string;
          tenantName: string;
          userId: string;
          name: string;
          email: string;
          role: string;
          isActive: boolean;
          lastLoginAt?: string | null;
          user: UserInfo;
      }
    | {
          kind: 'tenant';
          key: string;
          tenantSlug: string;
          tenantName: string;
          tenantId: string;
          userId: string;
          name: string;
          email: string;
          role: string;
          isActive: boolean;
          isOwner: boolean;
          lastLoginAt?: string | null;
          row: TenantUserRow;
      };
