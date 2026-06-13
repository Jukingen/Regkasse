import { describe, expect, it } from 'vitest';
import { MENU_PERMISSIONS } from '@/constants/permissions';
import { ROUTE_PERMISSIONS } from '@/shared/auth/routePermissions';

describe('menuPermissions constants', () => {
    it('aligns primary MENU_PERMISSIONS entries with ROUTE_PERMISSIONS', () => {
        for (const [path, required] of Object.entries(MENU_PERMISSIONS)) {
            expect(ROUTE_PERMISSIONS[path], `ROUTE_PERMISSIONS[${path}]`).toEqual(required);
        }
    });
});
