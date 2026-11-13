import { beforeEach, describe, expect, it } from 'vitest';

import { rememberAllowedAdminPath } from '@/shared/auth/useSafeNavigateBack';

describe('rememberAllowedAdminPath', () => {
  beforeEach(() => {
    sessionStorage.clear();
    localStorage.clear();
  });

  it('stores allowed paths except /403', () => {
    rememberAllowedAdminPath('/dashboard');
    expect(sessionStorage.getItem('rk_admin_last_allowed_path')).toBe('/dashboard');
    rememberAllowedAdminPath('/403');
    expect(sessionStorage.getItem('rk_admin_last_allowed_path')).toBe('/dashboard');
  });

  it('records recent menu leaves separately from the last-path pointer', () => {
    rememberAllowedAdminPath('/kassenverwaltung');
    rememberAllowedAdminPath('/dashboard');
    expect(JSON.parse(localStorage.getItem('rk_admin_recent_menu_paths') ?? '[]')).toEqual([
      '/kassenverwaltung',
    ]);
  });
});
