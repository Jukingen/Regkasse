import { NextResponse, NextRequest } from 'next/server';

/** Same name as client `authStorage` access key so HttpOnly migration stays aligned. */
const ACCESS_TOKEN_COOKIE = 'rk_admin_access_token';

const PUBLIC_PATHS = new Set(['/login', '/403', '/health', '/impersonate-callback']);

const PROTECTED_PREFIXES = ['/admin/', '/dashboard/', '/rksv/', '/settings/'] as const;

const PROTECTED_EXACT = new Set(['/admin', '/dashboard', '/rksv', '/settings']);

/** Clock skew leeway (seconds) when comparing JWT `exp` without signature verification. */
const EXP_LEEWAY_SEC = 60;

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
    | { ok: true; expired: false }
    | { ok: false; reason: 'format' | 'payload' | 'expired' | 'missing_exp' };

/**
 * Validates JWT shape (3 segments, decodable header/payload) and `exp` without verifying the signature.
 */
function validateJwtStructureAndExpiry(token: string): JwtDecodeResult {
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
    const now = Math.floor(Date.now() / 1000);
    if (exp + EXP_LEEWAY_SEC <= now) {
        return { ok: false, reason: 'expired' };
    }
    return { ok: true, expired: false };
}

function isPublicPath(pathname: string): boolean {
    return PUBLIC_PATHS.has(pathname);
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

export function proxy(request: NextRequest) {
    const { pathname } = request.nextUrl;

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

    if (isPublicPath(pathname)) {
        if (authenticated && rawToken) {
            return withForwardedToken(request, rawToken);
        }
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
    matcher: [
        '/((?!_next/static|_next/image|favicon.ico).*)',
    ],
};
