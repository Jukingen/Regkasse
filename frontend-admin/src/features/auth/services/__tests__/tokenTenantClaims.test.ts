import { describe, expect, it } from 'vitest';

import { readTokenTenantClaims } from '@/features/auth/services/tokenTenantClaims';

function fakeJwt(payload: Record<string, unknown>): string {
    const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }));
    const body = btoa(JSON.stringify(payload)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    return `${header}.${body}.sig`;
}

describe('readTokenTenantClaims', () => {
    it('reads impersonation and tenant slug from JWT', () => {
        const token = fakeJwt({
            tenant_impersonation: 'true',
            tenant_slug: 'cafe',
            tenant_id: '11111111-1111-1111-1111-111111111111',
        });
        const claims = readTokenTenantClaims(token);
        expect(claims.isImpersonating).toBe(true);
        expect(claims.tenantSlug).toBe('cafe');
        expect(claims.tenantId).toBe('11111111-1111-1111-1111-111111111111');
    });

    it('returns empty claims for missing token', () => {
        expect(readTokenTenantClaims(null)).toEqual({
            tenantId: null,
            tenantSlug: null,
            isImpersonating: false,
        });
    });
});
