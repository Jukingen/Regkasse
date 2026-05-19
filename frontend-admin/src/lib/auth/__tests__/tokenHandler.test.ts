import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import {
    applyImpersonationHandoffFromFragment,
    buildImpersonationRedirectUrl,
} from '@/lib/auth/tokenHandler';
import type { TenantImpersonationResponse } from '@/features/super-admin/api/adminTenants';

function b64urlJson(value: unknown): string {
    return Buffer.from(JSON.stringify(value)).toString('base64url');
}

function fakeJwt(payload: Record<string, unknown>): string {
    return `${b64urlJson({ alg: 'none' })}.${b64urlJson(payload)}.sig`;
}

describe('tokenHandler', () => {
    beforeEach(() => {
        window.localStorage.clear();
        vi.stubGlobal('history', {
            ...window.history,
            replaceState: vi.fn(),
        });
    });

    afterEach(() => {
        vi.unstubAllGlobals();
    });

    it('buildImpersonationRedirectUrl matches tenant callback fragment handoff', () => {
        vi.stubEnv('NEXT_PUBLIC_TENANT_APP_BASE_DOMAIN', 'regkasse.at');
        const res: TenantImpersonationResponse = {
            token: 'eyJ.test',
            expiresIn: 3600,
            tenantId: '00000000-0000-0000-0000-000000000001',
            tenantSlug: 'cafe',
            impersonation: true,
        };
        const url = buildImpersonationRedirectUrl(res);
        expect(url).toBe('https://cafe.regkasse.at/impersonate-callback#impersonate_token=eyJ.test&tenant=cafe');
        vi.unstubAllEnvs();
    });

    it('applyImpersonationHandoffFromFragment stores token and clears hash', () => {
        const token = fakeJwt({ tenant_impersonation: 'true', exp: Math.floor(Date.now() / 1000) + 3600 });
        const hash = `#impersonate_token=${encodeURIComponent(token)}&tenant=cafe`;
        const result = applyImpersonationHandoffFromFragment(hash, 'cafe');
        expect(result.ok).toBe(true);
        expect(window.localStorage.getItem('rk_admin_access_token')).toBe(token);
        expect(window.history.replaceState).toHaveBeenCalled();
    });
});
