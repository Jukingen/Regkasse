import { DEV_TENANT_PRESETS } from '../../constants/devTenantPresets';
import {
  getDevTenant,
  getEffectiveTenantSlug,
  getTenantSlugFromSubdomain,
  isLocalDevHostname,
} from '../devTenant';

describe('devTenant', () => {
  const originalNodeEnv = process.env.NODE_ENV;

  afterEach(() => {
    process.env.NODE_ENV = originalNodeEnv;
    window.localStorage.clear();
  });

  it('extracts tenant slug from production-style host', () => {
    expect(getTenantSlugFromSubdomain('acme.regkasse.at')).toBe('acme');
  });

  it('maps localhost to admin', () => {
    expect(getTenantSlugFromSubdomain('localhost')).toBe('admin');
  });

  it('uses localStorage override in development', () => {
    process.env.NODE_ENV = 'development';
    window.localStorage.setItem('dev_tenant_id', 'cafe');
    expect(getDevTenant()).toBe('cafe');
    expect(getEffectiveTenantSlug()).toBe('cafe');
    process.env.NODE_ENV = 'production';
    expect(getTenantSlugFromSubdomain('acme.regkasse.at')).toBe('acme');
  });

  it('uses regkasse.local subdomain when localStorage is empty', () => {
    process.env.NODE_ENV = 'development';
    Object.defineProperty(window, 'location', {
      value: { hostname: 'bar.regkasse.local' },
      configurable: true,
    });
    expect(isLocalDevHostname('bar.regkasse.local')).toBe(true);
    expect(getDevTenant()).toBe('bar');
  });

  it('exposes preset slugs for dev switcher', () => {
    expect(DEV_TENANT_PRESETS.map((p) => p.value)).toEqual(['dev', 'cafe', 'bar']);
  });
});
