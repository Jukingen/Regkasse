import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import type { CreateQuickUserResult } from '@/features/super-admin/api/quickUser';
import type { CreateUserResult } from '@/features/users/api/users';

export type CreateUserFormValues = {
  email: string;
  firstName?: string;
  lastName?: string;
  role: string;
  isOwner: boolean;
  tenantId?: string;
};

export type CreateUserQuickFormValues = {
  role: string;
  tenantId?: string;
};

export type CreateUserModalProps = {
  open: boolean;
  confirmLoading?: boolean;
  onClose: () => void;
  /** Called after the one-time password modal is dismissed. */
  onComplete?: () => void;
  /** Must resolve with create result (generated password) on success. */
  onSubmit: (values: CreateUserFormValues) => Promise<CreateUserResult>;
  /** Super Admin: show mandant picker when no fixed tenantId. */
  isSuperAdmin?: boolean;
  tenantId?: string;
  tenantRows?: AdminTenantListItem[];
  tenantsLoading?: boolean;
  showOwnerToggle?: boolean;
  variant?: 'tenantDetail' | 'usersPage';
  initialValues?: Partial<CreateUserFormValues>;
  allowDeferredTenantAssignment?: boolean;
  onAssignTenants?: (userId: string, tenantIds: string[]) => Promise<void>;
  quickMode?: {
    onSubmit: (values: CreateUserQuickFormValues) => Promise<CreateQuickUserResult>;
    onSubmitWithoutTenant?: (values: CreateUserQuickFormValues) => Promise<CreateUserResult>;
  };
};
