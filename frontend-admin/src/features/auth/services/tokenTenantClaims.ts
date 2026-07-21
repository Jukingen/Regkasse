import { authStorage } from '@/features/auth/services/authStorage';
import { decodeJwtPayload, isTruthyJwtClaim } from '@/lib/auth/jwtPayload';

export type TokenTenantClaims = {
  tenantId: string | null;
  tenantSlug: string | null;
  isImpersonating: boolean;
};

export function readTokenTenantClaims(token?: string | null): TokenTenantClaims {
  const accessToken = token ?? authStorage.getToken();
  if (!accessToken) {
    return { tenantId: null, tenantSlug: null, isImpersonating: false };
  }

  const payload = decodeJwtPayload(accessToken);
  if (!payload) {
    return { tenantId: null, tenantSlug: null, isImpersonating: false };
  }

  const tenantIdRaw = payload.tenant_id;
  const tenantSlugRaw = payload.tenant_slug ?? payload.tenantSlug;

  const tenantId =
    typeof tenantIdRaw === 'string' && tenantIdRaw.trim().length > 0 ? tenantIdRaw.trim() : null;
  const tenantSlug =
    typeof tenantSlugRaw === 'string' && tenantSlugRaw.trim().length > 0
      ? tenantSlugRaw.trim().toLowerCase()
      : null;

  return {
    tenantId,
    tenantSlug,
    isImpersonating: isTruthyJwtClaim(payload.tenant_impersonation),
  };
}
