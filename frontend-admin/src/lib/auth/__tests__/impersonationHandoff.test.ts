import { describe, expect, it, vi } from 'vitest';
import {
    buildTenantImpersonationRedirectUrl,
    parseImpersonationHandoffFromHash,
    shouldUseProductionImpersonationRedirect,
} from '@/lib/auth/impersonationHandoff';
import type { TenantImpersonationResponse } from '@/features/super-admin/api/adminTenants';

function b64urlJson(value: unknown): string {
    return Buffer.from(JSON.stringify(value)).toString('base64url');
}

function fakeJwt(payload: Record<string, unknown>): string {
    return `${b64urlJson({ alg: 'none' })}.${b64urlJson(payload)}.sig`;
}

describe('shouldUseProductionImpersonationRedirect', () => {
    it('returns false for localhost and *.local', () => {
        expect(shouldUseProductionImpersonationRedirect('localhost')).toBe(false);
        expect(shouldUseProductionImpersonationRedirect('127.0.0.1')).toBe(false);
        expect(shouldUseProductionImpersonationRedirect('dev.regkasse.local')).toBe(false);
    });

    it('returns true for production hosts', () => {
        expect(shouldUseProductionImpersonationRedirect('admin.regkasse.at')).toBe(true);
        expect(shouldUseProductionImpersonationRedirect('dev.regkasse.at')).toBe(true);
    });
});

describe('buildTenantImpersonationRedirectUrl', () => {
    it('builds fragment handoff URL on tenant subdomain', () => {
        vi.stubEnv('NEXT_PUBLIC_TENANT_APP_BASE_DOMAIN', 'regkasse.at');
        const res: TenantImpersonationResponse = {
            token: 'Bearer eyJ.test',
            expiresIn: 3600,
            refreshToken: 'refresh-abc',
            tenantId: '00000000-0000-0000-0000-000000000001',
            tenantSlug: 'dev',
            impersonation: true,
        };
        const url = buildTenantImpersonationRedirectUrl(res);
        expect(url.startsWith('https://dev.regkasse.at/impersonate-callback#')).toBe(true);
        expect(url).toContain('impersonate_token=eyJ.test');
        expect(url).toContain('refresh_token=refresh-abc');
        expect(url).toContain('tenant=dev');
        vi.unstubAllEnvs();
    });
});

describe('parseImpersonationHandoffFromHash', () => {
    it('accepts valid impersonation fragment', () => {
        const token = fakeJwt({ tenant_impersonation: 'true', exp: Math.floor(Date.now() / 1000) + 3600 });
        const hash = `#impersonate_token=${encodeURIComponent(token)}&tenant=dev`;
        const result = parseImpersonationHandoffFromHash(hash, 'dev');
        expect(result.ok).toBe(true);
        if (result.ok) {
            expect(result.payload.accessToken).toBe(token);
            expect(result.payload.tenantSlug).toBe('dev');
        }
    });

    it('rejects tenant mismatch', () => {
        const token = fakeJwt({ tenant_impersonation: true, exp: Math.floor(Date.now() / 1000) + 3600 });
        const hash = `#impersonate_token=${encodeURIComponent(token)}&tenant=bar`;
        const result = parseImpersonationHandoffFromHash(hash, 'dev');
        expect(result).toEqual({ ok: false, reason: 'tenant_mismatch' });
    });

    it('rejects missing impersonation claim', () => {
        const token = fakeJwt({ exp: Math.floor(Date.now() / 1000) + 3600 });
        const hash = `#impersonate_token=${encodeURIComponent(token)}&tenant=dev`;
        const result = parseImpersonationHandoffFromHash(hash, 'dev');
        expect(result).toEqual({ ok: false, reason: 'not_impersonation' });
    });
});
