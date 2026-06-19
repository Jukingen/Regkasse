import { describe, expect, it } from 'vitest';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import { isCommandItemAllowed, type CommandPalettePermissionContext } from '@/features/command-palette/commandPalettePermissions';
import type { CommandItem } from '@/features/command-palette/types';
import {
    MANAGER_ADMIN_PERMISSIONS,
    MANAGER_FORBIDDEN_MENU_KEYS,
    MANAGER_REQUIRED_MENU_KEYS,
} from '@/shared/__tests__/fixtures/adminAppPermissionFixtures';
import { canShowRksvMenu, canViewUsers, isSuperAdmin } from '@/features/auth/constants/roles';

function buildCtx(
    role: string,
    permissions: readonly string[],
    usePermissionFirst = true,
): CommandPalettePermissionContext {
    return {
        usePermissionFirst,
        permissions: [...permissions],
        userRole: role,
        isMenuItemAllowed,
        canViewUsers,
        canShowRksvMenu,
        canShowPlatformAdminMenu: isSuperAdmin,
    };
}

function pageItem(menuKey: string): CommandItem {
    return {
        id: `page:test:${menuKey}`,
        type: 'page',
        label: menuKey,
        description: menuKey,
        group: 'Navigation',
        keywords: [menuKey],
        menuKey,
        action: () => {},
    };
}

describe('commandPalettePermissions Manager oversight', () => {
    const managerCtx = buildCtx('Manager', MANAGER_ADMIN_PERMISSIONS);

    it('allows MANAGER_REQUIRED_MENU_KEYS navigation items', () => {
        const blocked = MANAGER_REQUIRED_MENU_KEYS.filter(
            (key) => !isCommandItemAllowed(pageItem(key), managerCtx),
        );
        expect(blocked, `Command palette blocks: ${blocked.join(', ')}`).toEqual([]);
    });

    it('blocks MANAGER_FORBIDDEN_MENU_KEYS including platform admin', () => {
        const allowed = MANAGER_FORBIDDEN_MENU_KEYS.filter((key) =>
            isCommandItemAllowed(pageItem(key), managerCtx),
        );
        expect(allowed, `Command palette should block: ${allowed.join(', ')}`).toEqual([]);
    });

    it('blocks platform routes for Manager even with cash_register.view', () => {
        const ctx = buildCtx('Manager', [MANAGER_ADMIN_PERMISSIONS[0], 'cash_register.view']);
        expect(isCommandItemAllowed(pageItem('/admin/tenants'), ctx)).toBe(false);
        expect(isCommandItemAllowed(pageItem('/admin/cash-registers'), ctx)).toBe(false);
    });

    it('allows RKSV routes via permission-first (not SuperAdmin role)', () => {
        expect(isCommandItemAllowed(pageItem('/rksv/compliance'), managerCtx)).toBe(true);
        expect(isCommandItemAllowed(pageItem('/rksv/operations'), managerCtx)).toBe(true);
    });

    it('legacy empty-permissions: Manager RKSV via role fallback', () => {
        const legacyCtx = buildCtx('Manager', [], false);
        expect(isCommandItemAllowed(pageItem('/rksv/compliance'), legacyCtx)).toBe(true);
        expect(isCommandItemAllowed(pageItem('/admin/tenants'), legacyCtx)).toBe(false);
    });
});
