import { describe, expect, it } from 'vitest';

import { getQuickUsernamePattern } from '@/features/super-admin/lib/quickUserPreview';

describe('getQuickUsernamePattern', () => {
    it('maps quick roles to backend username prefixes', () => {
        expect(getQuickUsernamePattern('Manager')).toBe('manager1 … manager999');
        expect(getQuickUsernamePattern('Cashier')).toBe('cashier1 … cashier999');
        expect(getQuickUsernamePattern('Accountant')).toBe('user1 … user999');
    });
});
