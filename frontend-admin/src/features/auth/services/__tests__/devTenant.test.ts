import {
  DEV_TENANT_CHANGED_EVENT,
  getDevTenant,
  getEffectiveTenantSlug,
  resolveTenantSlugForApiRequest,
  getTenantSlugFromSubdomain,
  isLocalDevHostname,
  writeDevTenantSlug,
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
    expect(resolveTenantSlugForApiRequest()).toBe('cafe');
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

  it('maps admin.regkasse.local to admin without implicit dev mandant', () => {
    process.env.NODE_ENV = 'development';
    Object.defineProperty(window, 'location', {
      value: { hostname: 'admin.regkasse.local' },
      configurable: true,
    });
    expect(getTenantSlugFromSubdomain('admin.regkasse.local')).toBe('admin');
    expect(getDevTenant()).toBe('admin');
    expect(getEffectiveTenantSlug()).toBe('admin');
  });

  it('dispatches DEV_TENANT_CHANGED_EVENT when dev slug changes', () => {
    process.env.NODE_ENV = 'development';
    const handler = vi.fn();
    window.addEventListener(DEV_TENANT_CHANGED_EVENT, handler);
    expect(writeDevTenantSlug('cafe')).toBe(true);
    expect(handler).toHaveBeenCalledTimes(1);
    expect(handler.mock.calls[0][0].detail).toEqual({ slug: 'cafe', previousSlug: null });
    expect(writeDevTenantSlug('cafe')).toBe(false);
    expect(handler).toHaveBeenCalledTimes(1);
    window.removeEventListener(DEV_TENANT_CHANGED_EVENT, handler);
  });
});
