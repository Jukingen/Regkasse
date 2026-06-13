'use client';

/**
 * Permission-filtered admin sidebar menu.
 * Built from `adminSidebarRegistry` + RKSV plugin; filtered via `filterSidebarMenuItems` / `usePermissions`.
 */

import React, { Suspense, useLayoutEffect, useMemo, useState } from 'react';
import { Menu, Typography, type MenuProps } from 'antd';
import { usePathname, useSearchParams } from 'next/navigation';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { isTenantLicenseBlockingModule } from '@/features/cash-registers/hooks/useCashRegisterModuleAccess';
import { useTenantLicenseStatus } from '@/features/license/hooks/useLicenseStatus';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import {
    canViewUsers,
    canShowRksvMenu,
    isSuperAdmin,
} from '@/features/auth/constants/roles';
import { OPERATOR_VERIFICATIONS_COPY } from '@/shared/operatorTruthCopy';
import { buildAdminSidebarMenuItems } from '@/shared/buildAdminSidebar';
import {
    collectSelectableRouteKeysFromMenuItems,
    computeSidebarOpenKeysMerge,
    filterSidebarMenuItems,
    resolveAdminMenuSelectedKeys,
    type SidebarPermissionContext,
} from '@/shared/adminSidebarNavigation';
import type { RksvMenuGroup } from '@/shared/rksvMenuModel';
import sidebarStyles from '@/app/(protected)/protected-layout-sidebar.module.css';

const EMPTY_PERMISSIONS: string[] = [];

export type AdminSidebarMenuPanelProps = {
    menuTheme: 'light' | 'dark';
    menuInlineCollapsed: boolean;
    isMobile: boolean;
    onNavigate?: () => void;
};

type AdminSidebarMenuInnerProps = AdminSidebarMenuPanelProps & {
    menuItems: MenuProps['items'];
    selectableRouteKeys: readonly string[];
    openKeys: string[];
    setOpenKeys: React.Dispatch<React.SetStateAction<string[]>>;
    withSearchParams: boolean;
};

export type UseAdminSidebarMenuResult = {
    menuItems: MenuProps['items'];
    selectableRouteKeys: string[];
    hasAccessibleMenus: boolean;
    canSeeRksv: boolean;
    rksvMenuGroups: RksvMenuGroup[];
    openKeys: string[];
    setOpenKeys: React.Dispatch<React.SetStateAction<string[]>>;
};

/** Builds registry menu tree and filters leaves by role / permission (`canViewMenu` contract). */
export function useAdminSidebarMenu(): UseAdminSidebarMenuResult {
    const pathname = usePathname();
    const { user } = useAuth();
    const { userPermissions } = usePermissions();
    const { t } = useI18n();

    const permissions = userPermissions.length > 0 ? userPermissions : EMPTY_PERMISSIONS;
    const usePermissionFirst = permissions.length > 0;
    const { isSuperAdminUser } = useCurrentTenant();
    const { data: tenantLicense } = useTenantLicenseStatus();
    const hideKassenverwaltungMenu = isTenantLicenseBlockingModule(tenantLicense, isSuperAdminUser);

    const { menuItems: allMenuItems, rksvMenuGroups } = useMemo(
        () =>
            buildAdminSidebarMenuItems({
                t,
                verificationNavLabel: OPERATOR_VERIFICATIONS_COPY.navMenuLabel,
            }),
        [t],
    );

    const canSeeRksv = useMemo(() => {
        if (usePermissionFirst) return isMenuItemAllowed('/rksv', permissions);
        return canShowRksvMenu(user?.role ?? '');
    }, [usePermissionFirst, permissions, user?.role]);

    const sidebarPermissionCtx = useMemo<SidebarPermissionContext>(
        () => ({
            usePermissionFirst,
            permissions,
            userRole: user?.role ?? '',
            isMenuItemAllowed,
            canViewUsers,
            canShowRksvMenu,
            canShowPlatformAdminMenu: (role) => isSuperAdmin(role),
            isSuperAdminRole: (role) => isSuperAdmin(role),
        }),
        [usePermissionFirst, permissions, user?.role],
    );

    const menuItems = useMemo(() => {
        const filtered = filterSidebarMenuItems(allMenuItems, sidebarPermissionCtx) ?? [];
        if (!hideKassenverwaltungMenu) {
            return filtered;
        }

        return filtered
            .map((item) => {
                if (!item || typeof item !== 'object' || !('children' in item) || !Array.isArray(item.children)) {
                    return item;
                }

                return {
                    ...item,
                    children: item.children.filter(
                        (child) =>
                            !child ||
                            typeof child !== 'object' ||
                            !('key' in child) ||
                            child.key !== '/kassenverwaltung',
                    ),
                };
            })
            .filter(
                (item) =>
                    !item ||
                    typeof item !== 'object' ||
                    !('key' in item) ||
                    item.key !== '/kassenverwaltung',
            );
    }, [allMenuItems, hideKassenverwaltungMenu, sidebarPermissionCtx]);

    const selectableRouteKeys = useMemo(
        () => collectSelectableRouteKeysFromMenuItems(menuItems),
        [menuItems],
    );

    const [openKeys, setOpenKeys] = useState<string[]>([]);

    useLayoutEffect(() => {
        setOpenKeys((prev) =>
            computeSidebarOpenKeysMerge({
                pathname,
                prevOpenKeys: prev,
                canSeeRksv,
                rksvGroups: rksvMenuGroups,
            }),
        );
    }, [pathname, canSeeRksv, rksvMenuGroups]);

    return {
        menuItems,
        selectableRouteKeys,
        hasAccessibleMenus: (menuItems?.length ?? 0) > 0,
        canSeeRksv,
        rksvMenuGroups,
        openKeys,
        setOpenKeys,
    };
}

function AdminSidebarMenuInner({
    menuTheme,
    menuInlineCollapsed,
    isMobile,
    onNavigate,
    menuItems,
    selectableRouteKeys,
    openKeys,
    setOpenKeys,
    withSearchParams,
}: AdminSidebarMenuInnerProps) {
    const pathname = usePathname();
    const searchParams = useSearchParams();
    const search = withSearchParams ? searchParams.toString() : undefined;

    const selectedKeys = useMemo(
        () => resolveAdminMenuSelectedKeys(pathname, selectableRouteKeys, search),
        [pathname, selectableRouteKeys, search],
    );

    return (
        <Menu
            theme={menuTheme}
            mode="inline"
            className={sidebarStyles.siderMenu}
            selectedKeys={selectedKeys}
            {...(menuInlineCollapsed ? {} : { openKeys, onOpenChange: setOpenKeys })}
            items={menuItems}
            inlineCollapsed={menuInlineCollapsed}
            onClick={() => {
                if (isMobile) onNavigate?.();
            }}
        />
    );
}

export function AdminSidebarBrand({
    collapsed,
    isMobile,
}: {
    collapsed: boolean;
    isMobile: boolean;
}) {
    const { t } = useI18n();

    return (
        <div
            style={{
                height: 64,
                margin: 16,
                background: 'rgba(0, 0, 0, 0.05)',
                borderRadius: 6,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontWeight: 'bold',
                overflow: 'hidden',
                whiteSpace: 'nowrap',
            }}
        >
            {collapsed && !isMobile
                ? t('adminShell.branding.sidebarCompact')
                : t('adminShell.branding.sidebarExpanded')}
        </div>
    );
}

export function AdminSidebarEmptyState() {
    const { t } = useI18n();

    return (
        <div style={{ padding: 20, textAlign: 'center' }}>
            <Typography.Text type="secondary">{t('adminShell.sidebar.noAccessibleMenus')}</Typography.Text>
        </div>
    );
}

/** Inline menu filtered by permissions; wraps search-param aware selected key resolution. */
export function AdminSidebarMenuPanel(props: AdminSidebarMenuPanelProps) {
    const {
        menuItems,
        selectableRouteKeys,
        hasAccessibleMenus,
        openKeys,
        setOpenKeys,
    } = useAdminSidebarMenu();

    if (!hasAccessibleMenus) {
        return <AdminSidebarEmptyState />;
    }

    return (
        <Suspense
            fallback={
                <AdminSidebarMenuInner
                    {...props}
                    menuItems={menuItems}
                    selectableRouteKeys={selectableRouteKeys}
                    openKeys={openKeys}
                    setOpenKeys={setOpenKeys}
                    withSearchParams={false}
                />
            }
        >
            <AdminSidebarMenuInner
                {...props}
                menuItems={menuItems}
                selectableRouteKeys={selectableRouteKeys}
                openKeys={openKeys}
                setOpenKeys={setOpenKeys}
                withSearchParams
            />
        </Suspense>
    );
}

/** Branding + permission-filtered menu (used inside layout Sider / mobile Drawer). */
export function AdminSidebar({
    collapsed,
    isMobile,
    menuTheme,
    menuInlineCollapsed,
    onNavigate,
}: AdminSidebarMenuPanelProps & {
    collapsed: boolean;
    isMobile: boolean;
}) {
    return (
        <>
            <AdminSidebarBrand collapsed={collapsed} isMobile={isMobile} />
            <AdminSidebarMenuPanel
                menuTheme={menuTheme}
                menuInlineCollapsed={menuInlineCollapsed}
                isMobile={isMobile}
                onNavigate={onNavigate}
            />
        </>
    );
}
