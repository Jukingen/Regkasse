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

const {
  resolveEffectiveTenantSlug,
  getEnvDevTenantSlug,
  appendTenantQueryParam,
  applyDevTenantAxiosParams,
} = require('../services/tenant/devTenant') as typeof import('../services/tenant/devTenant');

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

describe('appendTenantQueryParam', () => {
  it('adds ?tenant=slug to an absolute URL', () => {
    expect(appendTenantQueryParam('http://localhost:5184/api/health', 'dev')).toBe(
      'http://localhost:5184/api/health?tenant=dev'
    );
  });

  it('replaces an existing tenant query value', () => {
    expect(appendTenantQueryParam('http://localhost:5184/api/health?tenant=old', 'dev')).toBe(
      'http://localhost:5184/api/health?tenant=dev'
    );
  });

  it('appends with & when other query params exist', () => {
    expect(appendTenantQueryParam('http://localhost:5184/api/health?x=1', 'dev')).toContain(
      'tenant=dev'
    );
  });
});

describe('applyDevTenantAxiosParams', () => {
  it('merges tenant into a plain params object', () => {
    expect(applyDevTenantAxiosParams({ page: 1 }, 'dev')).toEqual({ page: 1, tenant: 'dev' });
  });

  it('sets tenant on URLSearchParams', () => {
    const next = applyDevTenantAxiosParams(new URLSearchParams('q=1'), 'dev');
    expect(next).toBeInstanceOf(URLSearchParams);
    expect((next as URLSearchParams).get('tenant')).toBe('dev');
  });
});
