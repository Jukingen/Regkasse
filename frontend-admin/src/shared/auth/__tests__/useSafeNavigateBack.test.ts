import { beforeEach, describe, expect, it, vi } from 'vitest';

import { rememberAllowedAdminPath } from '@/shared/auth/useSafeNavigateBack';

describe('rememberAllowedAdminPath', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('stores allowed paths except /403', () => {
    rememberAllowedAdminPath('/dashboard');
    expect(sessionStorage.getItem('rk_admin_last_allowed_path')).toBe('/dashboard');
    rememberAllowedAdminPath('/403');
    expect(sessionStorage.getItem('rk_admin_last_allowed_path')).toBe('/dashboard');
  });
});
