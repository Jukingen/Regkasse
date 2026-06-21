import { describe, expect, it } from 'vitest';

import { resolveRoleChangeTenantId } from '../resolveRoleChangeTenantId';

describe('resolveRoleChangeTenantId', () => {
    it('prefers active tenant when user is a member', () => {
        expect(
            resolveRoleChangeTenantId(
                [{ tenantId: 'tenant-a' }, { tenantId: 'tenant-b' }],
                'tenant-b',
            ),
        ).toBe('tenant-b');
    });

    it('returns sole membership when no preferred tenant', () => {
        expect(resolveRoleChangeTenantId([{ tenantId: 'tenant-a' }], null)).toBe('tenant-a');
    });

    it('falls back to preferred tenant when memberships are empty', () => {
        expect(resolveRoleChangeTenantId([], 'tenant-dev')).toBe('tenant-dev');
    });
});
