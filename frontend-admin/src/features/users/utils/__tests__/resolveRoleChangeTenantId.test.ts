import { describe, expect, it } from 'vitest';

import { resolveRoleChangeTenantId } from '../resolveRoleChangeTenantId';

describe('resolveRoleChangeTenantId', () => {
    it('prefers membership matching current tenant', () => {
        expect(
            resolveRoleChangeTenantId(
                [
                    { tenantId: 'tenant-a' },
                    { tenantId: 'tenant-b' },
                ],
                'tenant-b',
            ),
        ).toBe('tenant-b');
    });

    it('falls back to first membership', () => {
        expect(resolveRoleChangeTenantId([{ tenantId: 'tenant-a' }], null)).toBe('tenant-a');
    });

    it('uses current tenant when memberships list is empty', () => {
        expect(resolveRoleChangeTenantId([], 'tenant-dev')).toBe('tenant-dev');
    });
});
