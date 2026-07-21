import { NextRequest, NextResponse } from 'next/server';

import {
  CHANGE_PASSWORD_PATH,
  VOLUNTARY_CHANGE_PASSWORD_PATH,
} from '@/features/auth/constants/changePasswordRoute';

/** Same name as client `authStorage` access key so HttpOnly migration stays aligned. */
export const ACCESS_TOKEN_COOKIE = 'rk_admin_access_token';

const PUBLIC_PATHS = new Set([
  '/login',
  '/login/forgot-username',
  '/health',
  '/impersonate-callback',
  '/robots.txt',
  '/sitemap.xml',
  '/manifest.json',
  '/manifest.webmanifest',
  /** Mandatory first-login / temporary password change only. */
  CHANGE_PASSWORD_PATH,
]);

/** Unauthenticated monitoring sinks / probes (no secrets in responses). */
const PUBLIC_MONITORING_API_PATHS = new Set([
  '/api/monitoring/health',
  '/api/monitoring/web-vitals',
  '/api/monitoring/logs',
  '/api/monitoring/metrics',
]);

const PROTECTED_PREFIXES = [
  '/admin/',
  '/dashboard/',
  '/rksv/',
  '/settings/',
  '/staff/',
  '/users/',
  '/403',
] as const;

const PROTECTED_EXACT = new Set([
  '/admin',
  '/dashboard',
  '/rksv',
  '/settings',
  '/staff',
  '/users',
  '/403',
]);

/** Clock skew leeway (seconds) when comparing JWT `exp` without signature verification. */
export const EXP_LEEWAY_SEC = 60;

function stripBearer(value: string): string {
  const v = value.trim();
  return v.toLowerCase().startsWith('bearer ') ? v.slice(7).trim() : v;
}

function getRawToken(request: NextRequest): string | null {
  const auth = request.headers.get('authorization');
  if (auth) {
    const t = stripBearer(auth);
    if (t) return t;
  }
  const fromCookie = request.cookies.get(ACCESS_TOKEN_COOKIE)?.value;
  if (fromCookie) {
    const t = stripBearer(fromCookie);
    if (t) return t;
  }
  return null;
}

function base64UrlToJson(segment: string): unknown | null {
  try {
    let b64 = segment.replace(/-/g, '+').replace(/_/g, '/');
    const pad = b64.length % 4;
    if (pad) b64 += '='.repeat(4 - pad);
    const json = atob(b64);
    return JSON.parse(json) as unknown;
  } catch {
    return null;
  }
}

type JwtDecodeResult =
  | { ok: true; expired: false; mustChangePassword: boolean }
  | { ok: false; reason: 'format' | 'payload' | 'expired' | 'missing_exp' };

function readMustChangePasswordClaim(payload: object): boolean {
  const record = payload as Record<string, unknown>;
  const raw =
    record.must_change_password ??
    record.mustChangePasswordOnNextLogin ??
    record.MustChangePasswordOnNextLogin;
  return raw === true || raw === 'true' || raw === '1' || raw === 1;
}

/**
 * Validates JWT shape (3 segments, decodable header/payload) and `exp` without verifying the signature.
 * Optionally reads must-change-password claims when present (AuthGate remains source of truth via /me).
 */
export function validateJwtStructureAndExpiry(
  token: string,
  nowSec: number = Math.floor(Date.now() / 1000),
): JwtDecodeResult {
  const parts = token.split('.');
  if (parts.length !== 3 || !parts[0] || !parts[1] || !parts[2]) {
    return { ok: false, reason: 'format' };
  }
  const header = base64UrlToJson(parts[0]);
  const payload = base64UrlToJson(parts[1]);
  if (header === null || payload === null || typeof payload !== 'object' || payload === null) {
    return { ok: false, reason: 'payload' };
  }
  const exp = (payload as { exp?: unknown }).exp;
  if (typeof exp !== 'number' || !Number.isFinite(exp)) {
    return { ok: false, reason: 'missing_exp' };
  }
  if (exp + EXP_LEEWAY_SEC <= nowSec) {
    return { ok: false, reason: 'expired' };
  }
  return {
    ok: true,
    expired: false,
    mustChangePassword: readMustChangePasswordClaim(payload),
  };
}

export function isPublicPath(pathname: string): boolean {
  return PUBLIC_PATHS.has(pathname);
}

export function isPublicMonitoringApi(pathname: string): boolean {
  return PUBLIC_MONITORING_API_PATHS.has(pathname);
}

function isProtectedPath(pathname: string): boolean {
  if (PROTECTED_EXACT.has(pathname)) return true;
  return PROTECTED_PREFIXES.some((p) => pathname.startsWith(p));
}

function isApiPath(pathname: string): boolean {
  return pathname === '/api' || pathname.startsWith('/api/');
}

function withForwardedToken(request: NextRequest, token: string): NextResponse {
  const requestHeaders = new Headers(request.headers);
  const bearer = `Bearer ${token}`;
  if (!requestHeaders.has('authorization')) {
    requestHeaders.set('authorization', bearer);
  }
  requestHeaders.set('x-admin-access-token', token);
  return NextResponse.next({
    request: {
      headers: requestHeaders,
    },
  });
}

export function normalizePathname(pathname: string): string {
  return pathname.replace(/\/$/, '') || '/';
}

export function proxy(request: NextRequest) {
  const { pathname: rawPathname } = request.nextUrl;
  const pathname = normalizePathname(rawPathname);

  if (
    pathname.startsWith('/_next/') ||
    pathname === '/favicon.ico' ||
    /\.(?:ico|png|jpg|jpeg|gif|webp|svg|woff2?|ttf|eot)$/i.test(pathname)
  ) {
    return NextResponse.next();
  }

  const rawToken = getRawToken(request);
  const jwtResult = rawToken ? validateJwtStructureAndExpiry(rawToken) : null;
  const authenticated = jwtResult?.ok === true;
  const userMustChangePassword = jwtResult?.ok === true && jwtResult.mustChangePassword;

  // Forced change (temporary password): only when JWT claim is present.
  // Client AuthGate still enforces via /me when the claim is absent from the token.
  if (
    authenticated &&
    userMustChangePassword &&
    pathname !== CHANGE_PASSWORD_PATH &&
    !isApiPath(pathname)
  ) {
    return NextResponse.redirect(new URL(CHANGE_PASSWORD_PATH, request.url));
  }

  // Voluntary self-service password change — authenticated users without a force flag.
  if (
    pathname === VOLUNTARY_CHANGE_PASSWORD_PATH &&
    authenticated &&
    rawToken &&
    !userMustChangePassword
  ) {
    return withForwardedToken(request, rawToken);
  }

  if (isPublicPath(pathname)) {
    if (authenticated && rawToken) {
      return withForwardedToken(request, rawToken);
    }
    return NextResponse.next();
  }

  // Web Vitals / log / metrics beacons + health must work without a JWT.
  if (isPublicMonitoringApi(pathname)) {
    return NextResponse.next();
  }

  if (isApiPath(pathname)) {
    if (request.method === 'OPTIONS') {
      return NextResponse.next();
    }
    if (!authenticated || !rawToken) {
      return new NextResponse(null, { status: 401 });
    }
    return withForwardedToken(request, rawToken);
  }

  if (isProtectedPath(pathname)) {
    if (!authenticated || !rawToken) {
      const login = new URL('/login', request.url);
      return NextResponse.redirect(login);
    }
    return withForwardedToken(request, rawToken);
  }

  if (authenticated && rawToken) {
    return withForwardedToken(request, rawToken);
  }
  return NextResponse.next();
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico).*)'],
};
