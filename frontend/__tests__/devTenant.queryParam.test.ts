import { describe, expect, it } from '@jest/globals';

import {
  appendTenantQueryParam,
  applyDevTenantAxiosParams,
} from '../services/tenant/devTenant';

(global as typeof globalThis & { __DEV__?: boolean }).__DEV__ = true;

describe('appendTenantQueryParam', () => {
  it('adds tenant to an absolute URL', () => {
    expect(appendTenantQueryParam('http://localhost:5184/api/pos/payment', 'dev')).toBe(
      'http://localhost:5184/api/pos/payment?tenant=dev'
    );
  });

  it('replaces an existing tenant query', () => {
    expect(appendTenantQueryParam('http://localhost:5184/api/health?tenant=old', 'dev')).toContain(
      'tenant=dev'
    );
  });
});

describe('applyDevTenantAxiosParams', () => {
  it('sets tenant on a plain params object', () => {
    expect(applyDevTenantAxiosParams({ cashRegisterId: 'cr-1' }, 'dev')).toEqual({
      cashRegisterId: 'cr-1',
      tenant: 'dev',
    });
  });

  it('sets tenant when params are missing', () => {
    expect(applyDevTenantAxiosParams(undefined, 'dev')).toEqual({ tenant: 'dev' });
  });

  it('sets tenant on URLSearchParams', () => {
    const params = new URLSearchParams({ language: 'de' });
    const next = applyDevTenantAxiosParams(params, 'dev') as URLSearchParams;
    expect(next.get('language')).toBe('de');
    expect(next.get('tenant')).toBe('dev');
  });
});
