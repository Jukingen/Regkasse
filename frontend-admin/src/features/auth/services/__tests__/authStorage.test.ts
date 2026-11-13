import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import {
  ACCESS_TOKEN_COOKIE_NAME,
  EDGE_SESSION_COOKIE_NAME,
  authStorage,
} from '../authStorage';

describe('authStorage cookie session (no JWT in localStorage)', () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.sessionStorage.clear();
    document.cookie.split(';').forEach((part) => {
      const name = part.split('=')[0]?.trim();
      if (name) {
        document.cookie = `${name}=; Path=/; Max-Age=0`;
      }
    });
  });

  afterEach(() => {
    authStorage.removeToken();
  });

  it('setToken writes edge session cookie and expiry metadata, not the JWT', () => {
    const exp = Math.floor(Date.now() / 1000) + 3600;
    const payload = btoa(JSON.stringify({ exp, tenant_impersonation: false }));
    const jwt = `header.${payload}.signature`;
    authStorage.setToken(jwt);

    expect(authStorage.getToken()).toBeNull();
    expect(window.localStorage.getItem('rk_admin_access_token')).toBeNull();
    expect(document.cookie).not.toContain(jwt);
    expect(document.cookie).toContain(`${EDGE_SESSION_COOKIE_NAME}=1`);
    expect(authStorage.hasToken()).toBe(true);
    expect(authStorage.getAccessExpiresAtMs()).toBe(exp * 1000);
  });

  it('setTokens marks impersonation without storing refresh token', () => {
    authStorage.setTokens({
      accessToken: 'aaa.bbb.ccc',
      refreshToken: 'refresh-secret',
      impersonating: true,
    });

    expect(authStorage.getRefreshToken()).toBeNull();
    expect(window.localStorage.getItem('rk_admin_refresh_token')).toBeNull();
    expect(document.cookie).not.toContain('refresh-secret');
    expect(authStorage.isImpersonating()).toBe(true);
    expect(authStorage.hasToken()).toBe(true);
  });

  it('markSession writes only the compact edge cookie', () => {
    authStorage.markSession(new Date('2026-08-23T12:00:00Z'));
    expect(document.cookie).toContain(`${EDGE_SESSION_COOKIE_NAME}=1`);
    expect(document.cookie).not.toContain(`${ACCESS_TOKEN_COOKIE_NAME}=`);
    expect(authStorage.getAccessExpiresAtMs()).toBe(Date.parse('2026-08-23T12:00:00Z'));
  });

  it('removeToken clears edge cookie, legacy keys, tenant, theme, and preferences', () => {
    window.localStorage.setItem('rk_admin_access_token', 'legacy-jwt');
    window.localStorage.setItem('rk_admin_tenant_id', 'tenant-guid');
    window.localStorage.setItem('rk_admin_tenant_slug', 'dev');
    window.localStorage.setItem('regkasse.admin.personalization.v1', '{"themeMode":"dark"}');
    window.localStorage.setItem('themeMode', 'dark');
    window.localStorage.setItem('regkasse.admin.formatLocale', 'en-US');
    authStorage.markSession();
    authStorage.removeToken();

    expect(authStorage.hasToken()).toBe(false);
    expect(window.localStorage.getItem('rk_admin_access_token')).toBeNull();
    expect(window.localStorage.getItem('rk_admin_tenant_id')).toBeNull();
    expect(window.localStorage.getItem('rk_admin_tenant_slug')).toBeNull();
    expect(window.localStorage.getItem('regkasse.admin.personalization.v1')).toBeNull();
    expect(window.localStorage.getItem('themeMode')).toBeNull();
    expect(window.localStorage.getItem('regkasse.admin.formatLocale')).toBeNull();
    expect(document.cookie).not.toContain(`${EDGE_SESSION_COOKIE_NAME}=1`);
  });
});
