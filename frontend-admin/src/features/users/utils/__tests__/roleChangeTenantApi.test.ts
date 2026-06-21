import { describe, expect, it } from 'vitest';

import { isRoleChange, shouldUseTenantRoleChangeApi } from '../roleChangeTenantApi';

describe('roleChangeTenantApi', () => {
    describe('isRoleChange', () => {
        it('detects role changes case-insensitively', () => {
            expect(isRoleChange('Manager', 'Cashier')).toBe(true);
            expect(isRoleChange('Manager', 'manager')).toBe(false);
            expect(isRoleChange('', 'Cashier')).toBe(true);
        });
    });

    describe('shouldUseTenantRoleChangeApi', () => {
        it('uses tenant API for business role changes', () => {
            expect(shouldUseTenantRoleChangeApi('Manager', 'Cashier')).toBe(true);
        });

        it('skips tenant API for platform operators', () => {
            expect(shouldUseTenantRoleChangeApi('SuperAdmin', 'Manager')).toBe(false);
            expect(shouldUseTenantRoleChangeApi('Manager', 'SuperAdmin')).toBe(false);
        });

        it('skips when previous role is unknown', () => {
            expect(shouldUseTenantRoleChangeApi('', 'Cashier')).toBe(false);
        });

        it('skips when role is unchanged', () => {
            expect(shouldUseTenantRoleChangeApi('Manager', 'Manager')).toBe(false);
        });
    });
});
