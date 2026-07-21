import { describe, expect, it } from 'vitest';

import {
  buildQuickCreateRoleOptions,
  buildTenantCreateRoleOptions,
} from '@/features/users/components/CreateUserModal/roleOptions';

describe('CreateUserModal roleOptions', () => {
  const t = (key: string) => key;

  it('builds tenant create roles with i18n keys', () => {
    const options = buildTenantCreateRoleOptions(t);
    expect(options.some((o) => o.value === 'Manager')).toBe(true);
    expect(options.find((o) => o.value === 'Manager')?.label).toBe(
      'users.create.roleOptions.Manager.label'
    );
  });

  it('builds quick-create roles', () => {
    const options = buildQuickCreateRoleOptions(t);
    expect(options.length).toBeGreaterThan(0);
    expect(options.every((o) => typeof o.value === 'string' && typeof o.label === 'string')).toBe(
      true
    );
  });
});
