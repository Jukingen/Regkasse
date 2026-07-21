import { NextRequest } from 'next/server';
import { describe, expect, it } from 'vitest';

import {
  ACCESS_TOKEN_COOKIE,
  EXP_LEEWAY_SEC,
  isPublicPath,
  normalizePathname,
  proxy,
  validateJwtStructureAndExpiry,
} from '@/proxy';

function b64url(obj: unknown): string {
  const json = typeof obj === 'string' ? obj : JSON.stringify(obj);
  const b64 = Buffer.from(json, 'utf8').toString('base64');
  return b64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
}

function makeJwt(payload: Record<string, unknown>): string {
  return `${b64url({ alg: 'none', typ: 'JWT' })}.${b64url(payload)}.sig`;
}

function requestFor(path: string, init?: { cookie?: string; authorization?: string }): NextRequest {
  const headers = new Headers();
  if (init?.cookie) headers.set('cookie', init.cookie);
  if (init?.authorization) headers.set('authorization', init.authorization);
  return new NextRequest(new URL(path, 'http://admin.regkasse.local:3000'), { headers });
}

describe('proxy auth boundary', () => {
  it('treats login and health as public', () => {
    expect(isPublicPath('/login')).toBe(true);
    expect(isPublicPath('/health')).toBe(true);
    expect(isPublicPath('/force-password-change')).toBe(true);
    expect(isPublicPath('/robots.txt')).toBe(true);
    expect(isPublicPath('/sitemap.xml')).toBe(true);
    expect(isPublicPath('/manifest.json')).toBe(true);
    expect(isPublicPath('/manifest.webmanifest')).toBe(true);
    expect(isPublicPath('/backup')).toBe(false);
    expect(isPublicPath('/products')).toBe(false);
    expect(isPublicPath('/admin')).toBe(false);
    expect(isPublicPath('/rksv')).toBe(false);
  });

  it('normalizes trailing slashes', () => {
    expect(normalizePathname('/backup/')).toBe('/backup');
    expect(normalizePathname('/')).toBe('/');
  });

  describe('validateJwtStructureAndExpiry', () => {
    it('rejects malformed tokens', () => {
      expect(validateJwtStructureAndExpiry('a.b').ok).toBe(false);
      expect(validateJwtStructureAndExpiry('not-a-jwt').ok).toBe(false);
    });

    it('rejects expired tokens outside leeway', () => {
      const now = 1_700_000_000;
      const token = makeJwt({ exp: now - EXP_LEEWAY_SEC - 1, sub: 'u1' });
      const result = validateJwtStructureAndExpiry(token, now);
      expect(result).toEqual({ ok: false, reason: 'expired' });
    });

    it('accepts valid non-expired tokens and reads must-change claim', () => {
      const now = 1_700_000_000;
      const token = makeJwt({
        exp: now + 3600,
        must_change_password: true,
      });
      expect(validateJwtStructureAndExpiry(token, now)).toEqual({
        ok: true,
        expired: false,
        mustChangePassword: true,
      });
    });
  });

  it('redirects unauthenticated users from protected routes to /login', () => {
    const res = proxy(requestFor('/dashboard'));
    expect(res.status).toBe(307);
    expect(res.headers.get('location')).toBe('http://admin.regkasse.local:3000/login');
  });

  it('redirects unauthenticated users from /admin/tenants to /login', () => {
    const res = proxy(requestFor('/admin/tenants'));
    expect(res.status).toBe(307);
    expect(res.headers.get('location')).toContain('/login');
  });

  it('allows unauthenticated access to /login', () => {
    const res = proxy(requestFor('/login'));
    expect(res.status).toBe(200);
  });

  it('forwards authenticated cookie JWT on protected routes', () => {
    const now = Math.floor(Date.now() / 1000);
    const token = makeJwt({ exp: now + 3600, sub: 'u1' });
    const res = proxy(
      requestFor('/dashboard', {
        cookie: `${ACCESS_TOKEN_COOKIE}=${encodeURIComponent(token)}`,
      })
    );
    expect(res.status).toBe(200);
    expect(
      res.headers.get('x-middleware-next') || res.headers.get('x-middleware-rewrite') || 'ok'
    ).toBeTruthy();
  });

  it('returns 401 for unauthenticated /api calls', () => {
    const res = proxy(requestFor('/api/health-check-proxy'));
    expect(res.status).toBe(401);
  });

  it('allows unauthenticated monitoring health and metric beacons', () => {
    expect(proxy(requestFor('/api/monitoring/health')).status).toBe(200);
    expect(proxy(requestFor('/api/monitoring/metrics')).status).toBe(200);
    expect(proxy(requestFor('/api/monitoring/web-vitals')).status).toBe(200);
    expect(proxy(requestFor('/api/monitoring/logs')).status).toBe(200);
    expect(proxy(requestFor('/health')).status).toBe(200);
  });

  it('redirects to force-password-change when claim is set', () => {
    const now = Math.floor(Date.now() / 1000);
    const token = makeJwt({ exp: now + 3600, must_change_password: true });
    const res = proxy(
      requestFor('/dashboard', {
        cookie: `${ACCESS_TOKEN_COOKIE}=${encodeURIComponent(token)}`,
      })
    );
    expect(res.status).toBe(307);
    expect(res.headers.get('location')).toContain('/force-password-change');
  });
});
