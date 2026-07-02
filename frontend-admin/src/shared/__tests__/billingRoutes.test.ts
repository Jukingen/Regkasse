import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { getRequiredPermissionForPath } from '@/shared/auth/routePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { ROUTE_GUARD_PATHS_WITHOUT_SIDEBAR_LEAF } from '@/shared/adminSidebarRouteRegistry';
import { ADMIN_SIDEBAR_NON_RKSV_LEAF_ROUTE_KEYS } from '@/shared/adminSidebarRouteRegistry';

const BILLING_APP_DIR = join(process.cwd(), 'src/app/(protected)/admin/billing');

/** Expected Next.js App Router page modules under `/admin/billing`. */
const BILLING_PAGE_MODULES = [
    'page.tsx',
    'sales/page.tsx',
    'sales/new/page.tsx',
    'sales/[id]/page.tsx',
    'stats/page.tsx',
] as const;

describe('billing App Router registration', () => {
    it('includes all billing page modules', () => {
        for (const relativePath of BILLING_PAGE_MODULES) {
            expect(existsSync(join(BILLING_APP_DIR, relativePath)), relativePath).toBe(true);
        }
    });

    it('maps visible sidebar billing leaves to SYSTEM_CRITICAL', () => {
        const billingSidebarLeaves = ADMIN_SIDEBAR_NON_RKSV_LEAF_ROUTE_KEYS.filter((key) =>
            key.startsWith('/admin/billing'),
        );
        expect(billingSidebarLeaves.sort()).toEqual(['/admin/billing']);
        for (const route of billingSidebarLeaves) {
            expect(getRequiredPermissionForPath(route)).toEqual([PERMISSIONS.SYSTEM_CRITICAL]);
        }
    });

    it('guards deep links without sidebar leaves', () => {
        for (const route of ROUTE_GUARD_PATHS_WITHOUT_SIDEBAR_LEAF) {
            if (!route.startsWith('/admin/billing')) continue;
            expect(getRequiredPermissionForPath(route)).toEqual([PERMISSIONS.SYSTEM_CRITICAL]);
        }
    });

    it('guards dynamic sale detail via /admin/billing/sales prefix', () => {
        expect(
            getRequiredPermissionForPath('/admin/billing/sales/550e8400-e29b-41d4-a716-446655440000'),
        ).toEqual([PERMISSIONS.SYSTEM_CRITICAL]);
    });
});
