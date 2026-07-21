import { afterEach, beforeEach, describe, expect, it, jest } from '@jest/globals';

const mockConstants = {
  expoConfig: {
    name: 'Cash Register',
    slug: 'cash-register',
    version: '1.0.0',
  } as { name: string; slug: string; version: string } | null,
  nativeAppVersion: '9.9.9',
  executionEnvironment: 'standalone',
  manifest: { id: '@legacy/should-not-use' },
};

jest.mock('expo-constants', () => ({
  __esModule: true,
  default: mockConstants,
}));

describe('expoAppConstants', () => {
  beforeEach(() => {
    mockConstants.expoConfig = {
      name: 'Cash Register',
      slug: 'cash-register',
      version: '1.0.0',
    };
    mockConstants.nativeAppVersion = '9.9.9';
    mockConstants.executionEnvironment = 'standalone';
    jest.resetModules();
  });

  it('reads version from Constants.expoConfig (not deprecated manifest)', () => {
    const mod =
      require('../constants/expoAppConstants') as typeof import('../constants/expoAppConstants');

    expect(mod.getExpoConfig()?.version).toBe('1.0.0');
    expect(mod.getExpoAppVersionName()).toBe('1.0.0');
    expect(mod.getExpoAppSlug()).toBe('cash-register');
    // Guard: callers must never depend on deprecated Constants.manifest
    expect(mockConstants.manifest.id).toBe('@legacy/should-not-use');
  });

  it('falls back to nativeAppVersion when expoConfig is missing', () => {
    mockConstants.expoConfig = null;

    const { getExpoAppVersionName } =
      require('../constants/expoAppConstants') as typeof import('../constants/expoAppConstants');
    expect(getExpoAppVersionName('0.0.0')).toBe('9.9.9');
  });

  it('uses explicit fallback when no version sources exist', () => {
    mockConstants.expoConfig = null;
    mockConstants.nativeAppVersion = '';

    const { getExpoAppVersionName } =
      require('../constants/expoAppConstants') as typeof import('../constants/expoAppConstants');
    expect(getExpoAppVersionName('1.2.3')).toBe('1.2.3');
  });

  it('detects storeClient execution environment', () => {
    mockConstants.executionEnvironment = 'storeClient';

    const { isExpoStoreClient } =
      require('../constants/expoAppConstants') as typeof import('../constants/expoAppConstants');
    expect(isExpoStoreClient()).toBe(true);
  });
});

describe('expoPublicEnv', () => {
  const prevApi = process.env.EXPO_PUBLIC_API_BASE_URL;
  const prevTenant = process.env.EXPO_PUBLIC_DEV_TENANT_ID;

  beforeEach(() => {
    jest.resetModules();
  });

  afterEach(() => {
    if (prevApi == null) delete process.env.EXPO_PUBLIC_API_BASE_URL;
    else process.env.EXPO_PUBLIC_API_BASE_URL = prevApi;
    if (prevTenant == null) delete process.env.EXPO_PUBLIC_DEV_TENANT_ID;
    else process.env.EXPO_PUBLIC_DEV_TENANT_ID = prevTenant;
  });

  it('trims and drops empty public env values', () => {
    const { trimExpoPublicEnv, getExpoPublicEnvSnapshot } =
      require('../constants/expoPublicEnv') as typeof import('../constants/expoPublicEnv');

    expect(trimExpoPublicEnv('  https://api.example/api  ')).toBe('https://api.example/api');
    expect(trimExpoPublicEnv('   ')).toBeUndefined();
    expect(trimExpoPublicEnv(undefined)).toBeUndefined();

    process.env.EXPO_PUBLIC_API_BASE_URL = ' http://localhost:5184/api ';
    process.env.EXPO_PUBLIC_DEV_TENANT_ID = 'dev';

    const snap = getExpoPublicEnvSnapshot();
    expect(snap.apiBaseUrl).toBe('http://localhost:5184/api');
    expect(snap.devTenantId).toBe('dev');
  });
});
