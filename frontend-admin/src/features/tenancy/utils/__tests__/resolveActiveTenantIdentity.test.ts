import { describe, expect, it } from 'vitest';

import {
    resolveActiveTenantId,
    tenantSlugsMatch,
} from '../resolveActiveTenantIdentity';

describe('resolveActiveTenantIdentity', () => {
    it('matches canonical dev tenant slug aliases', () => {
        expect(tenantSlugsMatch('dev', 'test_cafe')).toBe(true);
        expect(tenantSlugsMatch('default', 'dev')).toBe(false);
    });

    it('prefers switcher row id over JWT', () => {
        expect(
            resolveActiveTenantId({
                resolvedRowId: 'dev-tenant-id',
                jwtTenantId: 'default-tenant-id',
                jwtTenantSlug: 'default',
                activeTenantSlug: 'dev',
            }),
        ).toBe('dev-tenant-id');
    });

    it('uses JWT id only when slug matches active tenant context', () => {
        expect(
            resolveActiveTenantId({
                resolvedRowId: null,
                jwtTenantId: 'dev-tenant-id',
                jwtTenantSlug: 'dev',
                activeTenantSlug: 'dev',
            }),
        ).toBe('dev-tenant-id');
    });

    it('does not fall back to JWT default id when active slug is dev', () => {
        expect(
            resolveActiveTenantId({
                resolvedRowId: null,
                jwtTenantId: '9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c',
                jwtTenantSlug: 'default',
                activeTenantSlug: 'dev',
            }),
        ).toBeNull();
    });
});
