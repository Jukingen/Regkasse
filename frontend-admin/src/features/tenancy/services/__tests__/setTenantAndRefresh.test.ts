import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { DEV_TENANT_LOCAL_STORAGE_KEY } from '@/features/auth/services/devTenant';
import { persistTenantSlugAndRefresh } from '@/features/tenancy/services/setTenantAndRefresh';

const mockBeginTenantSwitch = vi.fn();
const mockWriteDevTenantSlug = vi.fn();
const mockReload = vi.fn();

vi.mock('@/features/auth/services/tenantSwitchController', () => ({
  beginTenantSwitch: () => mockBeginTenantSwitch(),
}));

vi.mock('@/features/auth/services/devTenant', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/auth/services/devTenant')>();
  return {
    ...actual,
    isDevelopment: () => true,
    writeDevTenantSlug: (...args: unknown[]) => mockWriteDevTenantSlug(...args),
  };
});

describe('persistTenantSlugAndRefresh', () => {
  const originalNodeEnv = process.env.NODE_ENV;

  beforeEach(() => {
    vi.clearAllMocks();
    process.env.NODE_ENV = 'development';
    window.localStorage.clear();
    mockReload.mockImplementation(() => undefined);
    Object.defineProperty(window, 'location', {
      value: { reload: mockReload },
      configurable: true,
      writable: true,
    });
  });

  afterEach(() => {
    process.env.NODE_ENV = originalNodeEnv;
    window.localStorage.clear();
  });

  it('delegates slug + tenant id persistence to writeDevTenantSlug in development', () => {
    mockWriteDevTenantSlug.mockReturnValue(true);

    persistTenantSlugAndRefresh('dev', 'dev-tenant-id');

    expect(mockWriteDevTenantSlug).toHaveBeenCalledWith('dev', 'dev-tenant-id');
    expect(mockReload).not.toHaveBeenCalled();
  });

  it('reloads when slug is unchanged but tenant id is corrected', () => {
    mockWriteDevTenantSlug.mockReturnValue(false);
    window.localStorage.setItem(DEV_TENANT_LOCAL_STORAGE_KEY, 'dev');

    persistTenantSlugAndRefresh('dev', 'dev-tenant-id');

    expect(mockBeginTenantSwitch).toHaveBeenCalledTimes(1);
    expect(mockReload).toHaveBeenCalledTimes(1);
  });
});
