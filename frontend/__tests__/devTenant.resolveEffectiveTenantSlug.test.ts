import { jest, describe, it, expect, beforeEach, afterAll } from '@jest/globals';

import { secureStorage } from '../services/secureStorage';

jest.mock('../services/secureStorage', () => ({
  secureStorage: {
    getItem: jest.fn(async () => null),
    setItem: jest.fn(async () => undefined),
    removeItem: jest.fn(async () => undefined),
  },
}));

jest.mock('../services/tenant/tenantStorage', () => ({
  TENANT_HTTP_HEADER: 'X-Tenant-Id',
}));

// Avoid dynamic import + resetModules (needs experimental-vm-modules under some Jest runners).
(global as typeof globalThis & { __DEV__?: boolean }).__DEV__ = true;

const { resolveEffectiveTenantSlug, getEnvDevTenantSlug } =
  require('../services/tenant/devTenant') as typeof import('../services/tenant/devTenant');

describe('devTenant resolveEffectiveTenantSlug', () => {
  const prevDevTenantEnv = process.env.EXPO_PUBLIC_DEV_TENANT_ID;

  beforeEach(() => {
    jest.clearAllMocks();
    jest.mocked(secureStorage.getItem).mockResolvedValue(null);
    process.env.EXPO_PUBLIC_DEV_TENANT_ID = 'dev';
  });

  afterAll(() => {
    if (prevDevTenantEnv === undefined) {
      delete process.env.EXPO_PUBLIC_DEV_TENANT_ID;
    } else {
      process.env.EXPO_PUBLIC_DEV_TENANT_ID = prevDevTenantEnv;
    }
  });

  it('returns dev from EXPO_PUBLIC_DEV_TENANT_ID when no storage override', async () => {
    await expect(resolveEffectiveTenantSlug(null)).resolves.toBe('dev');
  });

  it('getEnvDevTenantSlug reads EXPO_PUBLIC_DEV_TENANT_ID', () => {
    process.env.EXPO_PUBLIC_DEV_TENANT_ID = 'prod';
    expect(getEnvDevTenantSlug()).toBe('prod');
  });

  it('defaults getEnvDevTenantSlug to dev when env is unset', () => {
    delete process.env.EXPO_PUBLIC_DEV_TENANT_ID;
    expect(getEnvDevTenantSlug()).toBe('dev');
  });
});
