import { QUICK_USER_ROLES } from '@/features/super-admin/api/quickUser';
import { TENANT_CREATE_ROLES } from '@/features/super-admin/api/tenantUsers';

const ROLE_I18N_KEYS: Record<string, string> = {
  Manager: 'users.create.roleOptions.Manager.label',
  Cashier: 'users.create.roleOptions.Cashier.label',
  Accountant: 'users.create.roleOptions.Accountant.label',
  Waiter: 'users.create.roleOptions.Waiter.label',
  Kitchen: 'users.create.roleOptions.Kitchen.label',
};

type Translate = (key: string, options?: { defaultValue?: string }) => string;

export function buildTenantCreateRoleOptions(t: Translate) {
  return TENANT_CREATE_ROLES.map((role) => ({
    value: role,
    label: t(ROLE_I18N_KEYS[role] ?? role, { defaultValue: role }),
  }));
}

export function buildQuickCreateRoleOptions(t: Translate) {
  return QUICK_USER_ROLES.map((role) => ({
    value: role,
    label: t(ROLE_I18N_KEYS[role] ?? role, { defaultValue: role }),
  }));
}
