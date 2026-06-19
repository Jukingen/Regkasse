import { isRksvRouteKeyAllowed } from '@/shared/auth/menuPermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import type { CommandItem } from '@/features/command-palette/types';

export type CommandPalettePermissionContext = {
    usePermissionFirst: boolean;
    permissions: string[];
    userRole: string;
    isMenuItemAllowed: (key: string, permissions: string[] | undefined) => boolean;
    canViewUsers: (role: string) => boolean;
    canManageUsers: (role: string) => boolean;
    canShowRksvMenu: (role: string) => boolean;
    canShowPlatformAdminMenu: (role: string) => boolean;
};

export function isCommandItemAllowed(
    item: CommandItem,
    ctx: CommandPalettePermissionContext,
): boolean {
    if (item.dynamic) return false;

    if (item.id === 'action:create-user') {
        if (ctx.usePermissionFirst) {
            return ctx.isMenuItemAllowed('/admin/users', ctx.permissions);
        }
        return ctx.canManageUsers(ctx.userRole);
    }
    if (item.id === 'action:create-platform-user') {
        return ctx.canShowPlatformAdminMenu(ctx.userRole);
    }
    if (item.id === 'action:create-register') {
        if (ctx.usePermissionFirst) {
            return ctx.isMenuItemAllowed('/kassenverwaltung', ctx.permissions);
        }
        return true;
    }
    if (item.id === 'action:trigger-backup') {
        if (ctx.usePermissionFirst) {
            return ctx.permissions.includes(PERMISSIONS.SETTINGS_VIEW);
        }
        return true;
    }

    const key = item.menuKey;
    if (!key) return true;

    if (ctx.usePermissionFirst) {
        if (
            key === '/admin/tenants' ||
            key === '/admin/licenses' ||
            key === '/admin/cash-registers'
        ) {
            return ctx.canShowPlatformAdminMenu(ctx.userRole);
        }
        return ctx.isMenuItemAllowed(key, ctx.permissions);
    }
    if (key === '/admin/users' || key === '/users') return ctx.canViewUsers(ctx.userRole);
    if (key.startsWith('/rksv')) return isRksvRouteKeyAllowed(key, ctx.permissions, ctx.userRole);
    if (
        key === '/admin/tenants' ||
        key === '/admin/licenses' ||
        key === '/admin/cash-registers'
    ) {
        return ctx.canShowPlatformAdminMenu(ctx.userRole);
    }
    return true;
}
