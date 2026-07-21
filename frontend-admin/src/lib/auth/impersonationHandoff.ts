import type { TenantImpersonationResponse } from '@/features/super-admin/api/adminTenants';
import { decodeJwtPayload } from '@/lib/auth/jwtPayload';

/** Fragment keys for cross-subdomain impersonation token handoff (Option A). */
export const IMPERSONATE_TOKEN_HASH_KEY = 'impersonate_token';
export const IMPERSONATE_REFRESH_HASH_KEY = 'refresh_token';
export const IMPERSONATE_TENANT_HASH_KEY = 'tenant';

export const IMPERSONATE_CALLBACK_PATH = '/impersonate-callback';

const DEFAULT_TENANT_APP_BASE_DOMAIN = 'regkasse.at';

export type ImpersonationHandoffPayload = {
  accessToken: string;
  refreshToken: string | null;
  tenantSlug: string;
  tenantId: string | null;
};

export type ImpersonationHandoffParseResult =
  | { ok: true; payload: ImpersonationHandoffPayload }
  | {
      ok: false;
      reason:
        | 'missing_token'
        | 'missing_tenant'
        | 'tenant_mismatch'
        | 'invalid_token'
        | 'not_impersonation'
        | 'expired';
    };

/** Production redirect: real host, not loopback, not *.local dev hosts. */
export function shouldUseProductionImpersonationRedirect(hostname?: string): boolean {
  const host = (hostname ?? (typeof window !== 'undefined' ? window.location.hostname : ''))
    .trim()
    .toLowerCase();
  if (!host || host === 'localhost' || host.startsWith('127.0.0.1')) {
    return false;
  }
  if (host.endsWith('.local')) {
    return false;
  }
  return true;
}

export function getTenantAppBaseDomain(): string {
  const fromEnv = process.env.NEXT_PUBLIC_TENANT_APP_BASE_DOMAIN?.trim();
  return fromEnv && fromEnv.length > 0 ? fromEnv : DEFAULT_TENANT_APP_BASE_DOMAIN;
}

export function buildTenantSubdomainOrigin(
  tenantSlug: string,
  protocol: 'https' | 'http' = 'https'
): string {
  const slug = tenantSlug.trim().toLowerCase();
  const baseDomain = getTenantAppBaseDomain();
  return `${protocol}://${slug}.${baseDomain}`;
}

/** Redirect target on tenant FA host; JWT travels in URL fragment (not sent to server). */
export function buildTenantImpersonationRedirectUrl(res: TenantImpersonationResponse): string {
  const params = new URLSearchParams();
  params.set(IMPERSONATE_TOKEN_HASH_KEY, stripBearerPrefix(res.token));
  if (res.refreshToken?.trim()) {
    params.set(IMPERSONATE_REFRESH_HASH_KEY, res.refreshToken.trim());
  }
  params.set(IMPERSONATE_TENANT_HASH_KEY, res.tenantSlug.trim());
  const origin = buildTenantSubdomainOrigin(res.tenantSlug);
  return `${origin}${IMPERSONATE_CALLBACK_PATH}#${params.toString()}`;
}

export function parseImpersonationHandoffFromHash(
  hash: string,
  expectedTenantSlug: string
): ImpersonationHandoffParseResult {
  const raw = hash.startsWith('#') ? hash.slice(1) : hash;
  if (!raw.trim()) {
    return { ok: false, reason: 'missing_token' };
  }

  const params = new URLSearchParams(raw);
  const accessToken = params.get(IMPERSONATE_TOKEN_HASH_KEY)?.trim() ?? '';
  const tenantSlug = params.get(IMPERSONATE_TENANT_HASH_KEY)?.trim().toLowerCase() ?? '';
  const refreshToken = params.get(IMPERSONATE_REFRESH_HASH_KEY)?.trim() ?? null;

  if (!accessToken) {
    return { ok: false, reason: 'missing_token' };
  }
  if (!tenantSlug) {
    return { ok: false, reason: 'missing_tenant' };
  }

  const expected = expectedTenantSlug.trim().toLowerCase();
  if (expected && tenantSlug !== expected) {
    return { ok: false, reason: 'tenant_mismatch' };
  }

  const payload = decodeJwtPayload(accessToken);
  if (!payload) {
    return { ok: false, reason: 'invalid_token' };
  }

  const impersonation = payload.tenant_impersonation;
  if (impersonation !== true && impersonation !== 'true') {
    return { ok: false, reason: 'not_impersonation' };
  }

  const exp = payload.exp;
  if (typeof exp === 'number' && Number.isFinite(exp)) {
    const now = Math.floor(Date.now() / 1000);
    if (exp <= now) {
      return { ok: false, reason: 'expired' };
    }
  }

  const tenantIdClaim = payload.tenant_id;
  const tenantId =
    typeof tenantIdClaim === 'string' && tenantIdClaim.trim().length > 0
      ? tenantIdClaim.trim()
      : null;

  return {
    ok: true,
    payload: {
      accessToken,
      refreshToken: refreshToken && refreshToken.length > 0 ? refreshToken : null,
      tenantSlug,
      tenantId,
    },
  };
}

export function clearImpersonationHashFromUrl(): void {
  if (typeof window === 'undefined') {
    return;
  }
  const path = `${window.location.pathname}${window.location.search}`;
  window.history.replaceState(null, '', path);
}

function stripBearerPrefix(token: string): string {
  const trimmed = token.trim();
  return trimmed.toLowerCase().startsWith('bearer ') ? trimmed.slice(7).trim() : trimmed;
}
