/**
 * Impersonation JWT handoff between admin host and tenant subdomain FA.
 * Tokens travel in the URL fragment (never sent to the server on navigation).
 */
import {
  type ImpersonationHandoffParseResult,
  buildTenantImpersonationRedirectUrl,
  clearImpersonationHashFromUrl,
  parseImpersonationHandoffFromHash,
  shouldUseProductionImpersonationRedirect,
} from '@/features/auth/lib/impersonationHandoff';
import { authStorage } from '@/features/auth/services/authStorage';
import { tenantStorage } from '@/features/auth/services/tenantStorage';

export {
  clearImpersonationHashFromUrl,
  getTenantAppBaseDomain,
  IMPERSONATE_CALLBACK_PATH,
  IMPERSONATE_REFRESH_HASH_KEY,
  IMPERSONATE_TENANT_HASH_KEY,
  IMPERSONATE_TOKEN_HASH_KEY,
  type ImpersonationHandoffParseResult,
  type ImpersonationHandoffPayload,
  parseImpersonationHandoffFromHash,
  shouldUseProductionImpersonationRedirect,
} from '@/features/auth/lib/impersonationHandoff';

/** Alias for task/docs naming; builds `https://{slug}.{base}/impersonate-callback#…`. */
export const buildImpersonationRedirectUrl = buildTenantImpersonationRedirectUrl;

/**
 * Parses fragment, persists tokens + tenant bootstrap, strips hash from the address bar.
 * Returns parse result when validation fails (caller shows error UI).
 */
export function applyImpersonationHandoffFromFragment(
  hash: string,
  expectedTenantSlug: string
): ImpersonationHandoffParseResult {
  const result = parseImpersonationHandoffFromHash(hash, expectedTenantSlug);
  clearImpersonationHashFromUrl();

  if (!result.ok) {
    return result;
  }

  authStorage.setToken(result.payload.accessToken);
  if (result.payload.refreshToken) {
    authStorage.setRefreshToken(result.payload.refreshToken);
  }
  tenantStorage.persistBootstrap({
    tenantId: result.payload.tenantId,
    tenantSlug: result.payload.tenantSlug,
  });

  return result;
}
