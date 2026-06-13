import { describe, expect, it } from 'vitest';
import type { MenuProps } from 'antd';
import { buildAdminSidebarMenuItems } from '@/shared/buildAdminSidebar';
import { filterSidebarMenuItems, type SidebarPermissionContext } from '@/shared/adminSidebarNavigation';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import {
    canShowPlatformAdminMenu,
    canShowRksvMenu,
    canViewUsers,
    isSuperAdmin,
} from '@/features/auth/constants/roles';
import {
    CASHIER_ADMIN_PERMISSIONS,
    CASHIER_FORBIDDEN_MENU_KEYS,
    CASHIER_REQUIRED_MENU_KEYS,
    MANAGER_ADMIN_PERMISSIONS,
    MANAGER_REQUIRED_MENU_KEYS,
} from './fixtures/adminAppPermissionFixtures';

const passthroughT = (k: string) => k;

function collectRoutableLeafKeys(items: MenuProps['items'] | undefined): string[] {
    const keys: string[] = [];
    for (const it of items ?? []) {
        if (!it || typeof it !== 'object') continue;
        if ('type' in it && it.type === 'divider') continue;
        const node = it as { key?: string; children?: MenuProps['items'] };
        if (node.children?.length) {
            keys.push(...collectRoutableLeafKeys(node.children));
            continue;
        }
        const key = node.key;
        if (typeof key !== 'string') continue;
        if (key.startsWith('grp-') || key.startsWith('rksv-grp-')) continue;
        keys.push(key);
    }
    return keys;
}

function buildSidebarCtx(role: string, permissions: readonly string[]): SidebarPermissionContext {
    return {
        usePermissionFirst: true,
        permissions: [...permissions],
        userRole: role,
        isMenuItemAllowed,
        canViewUsers,
        canShowRksvMenu,
        canShowPlatformAdminMenu,
        isSuperAdminRole: isSuperAdmin,
    };
}

function visibleMenuKeysForRole(role: string, permissions: readonly string[]): string[] {
    const { menuItems } = buildAdminSidebarMenuItems({
        t: passthroughT,
        verificationNavLabel: 'Verifications',
    });
    const filtered = filterSidebarMenuItems(menuItems, buildSidebarCtx(role, permissions)) ?? [];
    return collectRoutableLeafKeys(filtered);
}

describe('adminRoleMenuVisibility contract', () => {
    it('Cashier admin session hides POS-only menus', () => {
        const keys = visibleMenuKeysForRole('Cashier', CASHIER_ADMIN_PERMISSIONS);
        for (const forbidden of CASHIER_FORBIDDEN_MENU_KEYS) {
            expect(keys, `Cashier must not see ${forbidden}`).not.toContain(forbidden);
        }
    });

    it('Cashier admin session shows dashboard, catalog, payments, reports', () => {
        const keys = visibleMenuKeysForRole('Cashier', CASHIER_ADMIN_PERMISSIONS);
        for (const required of CASHIER_REQUIRED_MENU_KEYS) {
            expect(keys, `Cashier must see ${required}`).toContain(required);
        }
    });

    it('Manager admin session shows core Verwaltung and RKSV menus', () => {
        const keys = visibleMenuKeysForRole('Manager', MANAGER_ADMIN_PERMISSIONS);
        for (const required of MANAGER_REQUIRED_MENU_KEYS) {
            expect(keys, `Manager must see ${required}`).toContain(required);
        }
    });

    it('SuperAdmin sees platform admin leaves without explicit permissions', () => {
        const keys = visibleMenuKeysForRole('SuperAdmin', []);
        expect(keys).toContain('/admin/tenants');
        expect(keys).toContain('/dashboard');
    });
});
