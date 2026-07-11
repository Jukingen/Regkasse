import { jest, describe, it, expect, beforeEach, afterAll } from '@jest/globals';
import { storage } from '@/utils/storage';

jest.mock('@/utils/storage', () => ({
  storage: {
    getItem: jest.fn(),
    setItem: jest.fn(),
    removeItem: jest.fn(),
  },
}));

async function loadDevTenantModule() {
  (global as typeof globalThis & { __DEV__?: boolean }).__DEV__ = true;
  jest.resetModules();
  return import('@/services/tenant/devTenant');
}

describe('devTenant resolveEffectiveTenantSlug', () => {
  const prevDevTenantEnv = process.env.EXPO_PUBLIC_DEV_TENANT_ID;

  beforeEach(() => {
    jest.clearAllMocks();
    (storage.getItem as jest.Mock).mockResolvedValue(null);
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
    const {
      resolveEffectiveTenantSlug,
      DEV_TENANT_LOCAL_STORAGE_KEY,
    } = await loadDevTenantModule();

    await expect(resolveEffectiveTenantSlug(null)).resolves.toBe('dev');
    expect(storage.getItem).toHaveBeenCalledWith(DEV_TENANT_LOCAL_STORAGE_KEY);
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

  it('prefers storage override over env', async () => {
    (storage.getItem as jest.Mock).mockImplementation(async (key: string) => {
      if (key === 'dev_tenant_id') return 'prod';
      return null;
    });

    const { resolveEffectiveTenantSlug } = await loadDevTenantModule();

    await expect(resolveEffectiveTenantSlug(null)).resolves.toBe('prod');
  });
});
