import { AxiosHeaders, type InternalAxiosRequestConfig } from 'axios';
import { describe, expect, it } from 'vitest';

import { TENANT_HTTP_HEADER } from '@/features/auth/services/tenantStorage';
import {
  applyAxiosRequestAuthHeaders,
  extractBearerTokenFromAuthorization,
  readAxiosHeader,
  shouldClearStoredTokenAfterPublicAuth401,
} from '@/lib/axiosRequestAuth';

function emptyConfig(overrides?: Partial<InternalAxiosRequestConfig>): InternalAxiosRequestConfig {
  return {
    headers: new AxiosHeaders(),
    url: '/api/admin/backup/runs',
    method: 'get',
    ...overrides,
  } as InternalAxiosRequestConfig;
}

describe('applyAxiosRequestAuthHeaders', () => {
  it('sets Authorization Bearer and X-Tenant-Id', () => {
    const config = applyAxiosRequestAuthHeaders(emptyConfig(), {
      tenantSlug: 'dev',
      accessToken: 'jwt-token',
      acceptLanguage: 'de',
    });

    expect(readAxiosHeader(config.headers, 'Authorization')).toBe('Bearer jwt-token');
    expect(readAxiosHeader(config.headers, TENANT_HTTP_HEADER)).toBe('dev');
    expect(readAxiosHeader(config.headers, 'Accept-Language')).toBe('de');
  });

  it('omits Authorization and tenant when missing', () => {
    const config = applyAxiosRequestAuthHeaders(emptyConfig(), {
      tenantSlug: '  ',
      accessToken: null,
      acceptLanguage: 'en',
    });

    expect(readAxiosHeader(config.headers, 'Authorization')).toBeUndefined();
    expect(readAxiosHeader(config.headers, TENANT_HTTP_HEADER)).toBeUndefined();
    expect(readAxiosHeader(config.headers, 'Accept-Language')).toBe('en');
  });

  it('merges CSRF extras without dropping auth headers', () => {
    const config = applyAxiosRequestAuthHeaders(emptyConfig(), {
      tenantSlug: 'cafe',
      accessToken: 'abc',
      acceptLanguage: 'tr',
      extraHeaders: { 'X-XSRF-TOKEN': 'csrf-1' },
    });

    expect(readAxiosHeader(config.headers, 'Authorization')).toBe('Bearer abc');
    expect(readAxiosHeader(config.headers, TENANT_HTTP_HEADER)).toBe('cafe');
    expect(readAxiosHeader(config.headers, 'X-XSRF-TOKEN')).toBe('csrf-1');
  });

  it('preserves an explicit X-Tenant-Id override', () => {
    const config = applyAxiosRequestAuthHeaders(
      emptyConfig({
        headers: new AxiosHeaders({ [TENANT_HTTP_HEADER]: 'selected-cafe' }),
        params: { tenant: 'selected-cafe' },
      }),
      {
        tenantSlug: 'dev',
        accessToken: null,
        acceptLanguage: 'de',
        injectDevTenantQuery: true,
      }
    );

    expect(readAxiosHeader(config.headers, TENANT_HTTP_HEADER)).toBe('selected-cafe');
    expect(config.params).toEqual({ tenant: 'selected-cafe' });
  });

  it('does not inject tenant query by default (header preferred)', () => {
    const withInject = applyAxiosRequestAuthHeaders(emptyConfig({ params: { page: 1 } }), {
      tenantSlug: 'dev',
      accessToken: null,
      acceptLanguage: 'de',
      injectDevTenantQuery: true,
    });
    expect(withInject.params).toEqual({ page: 1, tenant: 'dev' });

    const alreadySet = applyAxiosRequestAuthHeaders(emptyConfig({ params: { tenant: 'other' } }), {
      tenantSlug: 'dev',
      accessToken: null,
      acceptLanguage: 'de',
      injectDevTenantQuery: true,
    });
    expect(alreadySet.params).toEqual({ tenant: 'other' });

    const headerOnly = applyAxiosRequestAuthHeaders(emptyConfig({ params: { page: 1 } }), {
      tenantSlug: 'dev',
      accessToken: null,
      acceptLanguage: 'de',
      injectDevTenantQuery: false,
    });
    expect(headerOnly.params).toEqual({ page: 1 });
    expect(readAxiosHeader(headerOnly.headers, TENANT_HTTP_HEADER)).toBe('dev');
  });
});

describe('shouldClearStoredTokenAfterPublicAuth401', () => {
  it('clears when there is no current token', () => {
    expect(
      shouldClearStoredTokenAfterPublicAuth401({
        requestAuthorizationHeader: 'Bearer old',
        currentAccessToken: null,
      })
    ).toBe(true);
  });

  it('clears when the failed request used the current token', () => {
    expect(
      shouldClearStoredTokenAfterPublicAuth401({
        requestAuthorizationHeader: 'Bearer same-token',
        currentAccessToken: 'same-token',
      })
    ).toBe(true);
  });

  it('keeps a newer login token when a stale request 401 arrives', () => {
    expect(
      shouldClearStoredTokenAfterPublicAuth401({
        requestAuthorizationHeader: 'Bearer stale-token',
        currentAccessToken: 'fresh-login-token',
      })
    ).toBe(false);
  });

  it('keeps a stored token when the failed request had no Authorization', () => {
    expect(
      shouldClearStoredTokenAfterPublicAuth401({
        requestAuthorizationHeader: undefined,
        currentAccessToken: 'fresh-login-token',
      })
    ).toBe(false);
  });
});

describe('extractBearerTokenFromAuthorization', () => {
  it('strips Bearer prefix case-insensitively', () => {
    expect(extractBearerTokenFromAuthorization('Bearer abc.def')).toBe('abc.def');
    expect(extractBearerTokenFromAuthorization('bearer abc.def')).toBe('abc.def');
  });

  it('returns null for empty values', () => {
    expect(extractBearerTokenFromAuthorization(null)).toBeNull();
    expect(extractBearerTokenFromAuthorization('   ')).toBeNull();
  });
});
