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
    window.localStorage.setItem('dev_tenant_id', 'dev');
    expect(getDevTenant()).toBe('dev');
    expect(getEffectiveTenantSlug()).toBe('dev');
    expect(resolveTenantSlugForApiRequest()).toBe('dev');
    process.env.NODE_ENV = 'production';
    expect(getTenantSlugFromSubdomain('acme.regkasse.at')).toBe('acme');
  });

  it('uses regkasse.local subdomain when localStorage is empty', () => {
    process.env.NODE_ENV = 'development';
    Object.defineProperty(window, 'location', {
      value: { hostname: 'prod.regkasse.local' },
      configurable: true,
    });
    expect(isLocalDevHostname('prod.regkasse.local')).toBe(true);
    expect(getDevTenant()).toBe('prod');
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

  it('omits X-Tenant-Id on admin host without mandant override (JWT wins)', () => {
    process.env.NODE_ENV = 'development';
    Object.defineProperty(window, 'location', {
      value: { hostname: 'admin.regkasse.local' },
      configurable: true,
    });
    expect(resolveTenantSlugForApiRequest()).toBe('');
  });

  it('uses login bootstrap mandant on admin host when localStorage override is empty', () => {
    process.env.NODE_ENV = 'development';
    Object.defineProperty(window, 'location', {
      value: { hostname: 'localhost' },
      configurable: true,
    });
    window.localStorage.setItem('rk_admin_tenant_slug', 'dev');
    expect(resolveTenantSlugForApiRequest()).toBe('dev');
  });

  it('dispatches DEV_TENANT_CHANGED_EVENT when dev slug changes', () => {
    process.env.NODE_ENV = 'development';
    const handler = vi.fn();
    window.addEventListener(DEV_TENANT_CHANGED_EVENT, handler);
    expect(writeDevTenantSlug('dev')).toBe(true);
    expect(handler).toHaveBeenCalledTimes(1);
    expect(handler.mock.calls[0][0].detail).toEqual({ slug: 'dev', previousSlug: null });
    expect(writeDevTenantSlug('dev')).toBe(false);
    expect(handler).toHaveBeenCalledTimes(1);
    window.removeEventListener(DEV_TENANT_CHANGED_EVENT, handler);
  });
});
