import { describe, expect, it } from 'vitest';

import { isPathAllowedWithoutTenant } from '@/features/tenancy/hooks/useSuperAdminTenantMode';

describe('isPathAllowedWithoutTenant', () => {
    it('allows platform admin routes without mandant', () => {
        expect(isPathAllowedWithoutTenant('/admin')).toBe(true);
        expect(isPathAllowedWithoutTenant('/admin/tenants')).toBe(true);
        expect(isPathAllowedWithoutTenant('/admin/license')).toBe(true);
        expect(isPathAllowedWithoutTenant('/admin/system/time-sync')).toBe(true);
    });

    it('blocks mandant-scoped routes', () => {
        expect(isPathAllowedWithoutTenant('/admin/users')).toBe(true);
        expect(isPathAllowedWithoutTenant('/users')).toBe(false);
        expect(isPathAllowedWithoutTenant('/settings')).toBe(false);
        expect(isPathAllowedWithoutTenant('/products')).toBe(false);
        expect(isPathAllowedWithoutTenant('/dashboard')).toBe(false);
    });
});
