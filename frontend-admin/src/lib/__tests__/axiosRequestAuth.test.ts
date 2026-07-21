import { AxiosHeaders, type InternalAxiosRequestConfig } from 'axios';
import { describe, expect, it } from 'vitest';

import { TENANT_HTTP_HEADER } from '@/features/auth/services/tenantStorage';
import { applyAxiosRequestAuthHeaders, readAxiosHeader } from '@/lib/axiosRequestAuth';

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

  it('injects dev tenant query only when requested and unset', () => {
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

    const prod = applyAxiosRequestAuthHeaders(emptyConfig({ params: { page: 1 } }), {
      tenantSlug: 'dev',
      accessToken: null,
      acceptLanguage: 'de',
      injectDevTenantQuery: false,
    });
    expect(prod.params).toEqual({ page: 1 });
  });
});
