import { describe, it, expect } from 'vitest';

import { canAccessPath } from '@/shared/auth/canAccessPath';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { MANAGER_ADMIN_PERMISSIONS } from '@/shared/__tests__/fixtures/adminAppPermissionFixtures';

describe('canAccessPath', () => {
    it('allows Manager with finanzonline.manage to open Sonderbelege', () => {
        expect(canAccessPath('/rksv/sonderbelege', [...MANAGER_ADMIN_PERMISSIONS])).toBe(true);
    });

    it('denies Cashier without finanzonline.manage', () => {
        expect(
            canAccessPath('/rksv/sonderbelege', [
                PERMISSIONS.PAYMENT_VIEW,
                PERMISSIONS.REPORT_VIEW,
            ]),
        ).toBe(false);
    });

    it('allows dashboard for any user with permission claims', () => {
        expect(canAccessPath('/dashboard', [PERMISSIONS.PAYMENT_VIEW])).toBe(true);
    });

    it('denies unknown paths without route mapping', () => {
        expect(canAccessPath('/unknown/path', [PERMISSIONS.PAYMENT_VIEW])).toBe(false);
    });
});
