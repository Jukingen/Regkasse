import { describe, expect, it } from 'vitest';

import {
    canOfferPreservePermissions,
    getRoleChangePreserveAvailability,
    isSameRoleName,
    shouldPromptRoleChange,
} from '@/features/users/utils/roleChangePreservePolicy';

describe('roleChangePreservePolicy', () => {
    it('detects same role case-insensitively', () => {
        expect(isSameRoleName('Manager', 'manager')).toBe(true);
        expect(isSameRoleName('Cashier', 'Manager')).toBe(false);
    });

    it('returns same_role when roles match', () => {
        expect(getRoleChangePreserveAvailability('Manager', 'Manager')).toBe('same_role');
    });

    it('returns no_previous_role when previous role is missing', () => {
        expect(getRoleChangePreserveAvailability('', 'Cashier')).toBe('no_previous_role');
        expect(getRoleChangePreserveAvailability(null, 'Cashier')).toBe('no_previous_role');
    });

    it('returns superadmin_source for SuperAdmin previous role', () => {
        expect(getRoleChangePreserveAvailability('SuperAdmin', 'Manager')).toBe('superadmin_source');
    });

    it('returns available for normal role changes', () => {
        expect(getRoleChangePreserveAvailability('Manager', 'Cashier')).toBe('available');
    });

    it('offers preserve only when available and tenant context exists', () => {
        expect(canOfferPreservePermissions('Manager', 'Cashier', true)).toBe(true);
        expect(canOfferPreservePermissions('Manager', 'Cashier', false)).toBe(false);
        expect(canOfferPreservePermissions('SuperAdmin', 'Manager', true)).toBe(false);
    });

    it('prompts role change except for same role', () => {
        expect(shouldPromptRoleChange('Manager', 'Cashier')).toBe(true);
        expect(shouldPromptRoleChange('Manager', 'Manager')).toBe(false);
        expect(shouldPromptRoleChange('', 'Cashier')).toBe(true);
    });
});
