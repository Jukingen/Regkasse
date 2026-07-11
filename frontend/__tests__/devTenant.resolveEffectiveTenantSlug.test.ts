import { jest, describe, it, expect, beforeEach, afterAll } from '@jest/globals';
import { storage } from '../utils/storage';

jest.mock('../utils/storage', () => ({
  storage: {
    getItem: jest.fn(),
    setItem: jest.fn(),
    removeItem: jest.fn(),
  },
}));

async function loadDevTenantModule() {
  (global as typeof globalThis & { __DEV__?: boolean }).__DEV__ = true;
  jest.resetModules();
  return import('../services/tenant/devTenant');
}

describe('devTenant resolveEffectiveTenantSlug', () => {
  const prevDevTenantEnv = process.env.EXPO_PUBLIC_DEV_TENANT_ID;

  beforeEach(() => {
    jest.clearAllMocks();
    jest.mocked(storage.getItem).mockResolvedValue(null);
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
    const { resolveEffectiveTenantSlug } = await loadDevTenantModule();

    await expect(resolveEffectiveTenantSlug(null)).resolves.toBe('dev');
  });

  it('getEnvDevTenantSlug reads EXPO_PUBLIC_DEV_TENANT_ID', async () => {
    process.env.EXPO_PUBLIC_DEV_TENANT_ID = 'prod';
    const { getEnvDevTenantSlug } = await loadDevTenantModule();

    expect(getEnvDevTenantSlug()).toBe('prod');
  });

  it('defaults getEnvDevTenantSlug to dev when env is unset', async () => {
    delete process.env.EXPO_PUBLIC_DEV_TENANT_ID;
    const { getEnvDevTenantSlug } = await loadDevTenantModule();

    expect(getEnvDevTenantSlug()).toBe('dev');
  });
});
