import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { buildAdminPlatformOrigin, exitImpersonation } from '@/features/auth/lib/exitImpersonation';
import { authStorage } from '@/features/auth/services/authStorage';
import { DEV_TENANT_LOCAL_STORAGE_KEY } from '@/features/auth/services/devTenant';

describe('exitImpersonation', () => {
  const assign = vi.fn();

  beforeEach(() => {
    assign.mockReset();
    vi.stubGlobal('location', {
      hostname: 'dev.regkasse.at',
      protocol: 'https:',
      assign,
    } as Location);
    localStorage.clear();
    authStorage.setToken('test.jwt.token');
    localStorage.setItem(DEV_TENANT_LOCAL_STORAGE_KEY, 'dev');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it('buildAdminPlatformOrigin uses admin slug', () => {
    expect(buildAdminPlatformOrigin()).toMatch(/^https:\/\/admin\./);
  });

  it('clears session and redirects to admin tenants in production', () => {
    exitImpersonation();

    expect(authStorage.getToken()).toBeNull();
    expect(localStorage.getItem(DEV_TENANT_LOCAL_STORAGE_KEY)).toBeNull();
    expect(assign).toHaveBeenCalledWith(`${buildAdminPlatformOrigin('https')}/admin/tenants`);
  });

  it('redirects same-origin on localhost dev', () => {
    vi.stubGlobal('location', {
      hostname: 'localhost',
      protocol: 'http:',
      assign,
    } as Location);

    exitImpersonation();

    expect(assign).toHaveBeenCalledWith('/admin/tenants');
  });
});
