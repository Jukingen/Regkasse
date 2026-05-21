import { describe, expect, it } from 'vitest';

import {
    buildAdminUsersPageHref,
    readTenantIdFromSearchParams,
    resolveAdminUsersTenantFilterFromSearchParams,
    ADMIN_USERS_FILTER_PLATFORM,
} from '../adminUsersPageUrl';

describe('adminUsersPageUrl', () => {
    it('builds href with tenantId query', () => {
        expect(buildAdminUsersPageHref('abc-123')).toBe('/admin/users?tenantId=abc-123');
        expect(buildAdminUsersPageHref()).toBe('/admin/users');
    });

    it('reads tenantId and legacy tenant from search params', () => {
        expect(readTenantIdFromSearchParams(new URLSearchParams('tenantId=x'))).toBe('x');
        expect(readTenantIdFromSearchParams(new URLSearchParams('tenant=y'))).toBe('y');
        expect(readTenantIdFromSearchParams(new URLSearchParams('tenantId=x&tenant=y'))).toBe('x');
    });

    it('resolves platform filter from URL', () => {
        expect(resolveAdminUsersTenantFilterFromSearchParams(new URLSearchParams('filter=platform'))).toBe(
            ADMIN_USERS_FILTER_PLATFORM,
        );
        expect(resolveAdminUsersTenantFilterFromSearchParams(new URLSearchParams('tenantId=cafe-id'))).toBe(
            'cafe-id',
        );
    });
});
