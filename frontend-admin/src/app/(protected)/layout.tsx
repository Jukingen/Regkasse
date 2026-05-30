'use client';

/**
 * Protected admin shell: sidebar menu is built from `buildAdminSidebarMenuItems` (registry + RKSV plugin).
 * Permissions: `MENU_PERMISSION` / `ROUTE_PERMISSIONS`. Desktop Sider width: `usePersistedAdminSiderWidth`.
 */

import React, { Suspense, useState, useEffect, useMemo, useLayoutEffect, ReactNode } from 'react';
import { Layout, Menu, theme, Drawer, Grid, MenuProps } from 'antd';
import { UserOutlined, LogoutOutlined } from '@ant-design/icons';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { AuthGate } from '@/shared/auth/AuthGate';
import { PermissionRouteGuard } from '@/shared/auth/PermissionRouteGuard';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import {
    canViewUsers,
    canShowRksvMenu,
    isSuperAdmin,
} from '@/features/auth/constants/roles';
import { OPERATOR_VERIFICATIONS_COPY } from '@/shared/operatorTruthCopy';
import { ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { usePersonalization } from '@/lib/personalization/PersonalizationProvider';
import {
    collectSelectableRouteKeysFromMenuItems,
    computeSidebarOpenKeysMerge,
    filterSidebarMenuItems,
    resolveAdminMenuSelectedKeys,
    type SidebarPermissionContext,
} from '@/shared/adminSidebarNavigation';
import { buildAdminSidebarMenuItems } from '@/shared/buildAdminSidebar';
import { PageLoader } from '@/components/ui/PageLoader';
import { ImpersonationBanner } from '@/components/admin-layout/ImpersonationBanner';
import { SuperAdminModeBanner } from '@/components/admin-layout/SuperAdminModeBanner';
import { SuperAdminTenantGate } from '@/components/admin-layout/SuperAdminTenantGate';
import { VerwaltungTenantContextGate } from '@/components/admin-layout/VerwaltungTenantContextGate';
import { AdminShellHeader } from '@/components/layout/Header';
import { CommandPaletteShell } from '@/components/CommandPalette';
import { PasswordChangeRequiredRedirect } from '@/features/auth/components/PasswordChangeRequiredRedirect';
import { AppLayout } from '@/components/AppLayout';
import { LicenseExpiryBanner } from '@/components/admin-layout/LicenseExpiryBanner';
import { AdminDesktopSiderResizeHandle } from '@/components/admin-layout/AdminDesktopSiderResizeHandle';
import {
    ADMIN_SIDER_WIDTH_MAX,
    ADMIN_SIDER_WIDTH_MIN,
    clampAdminSiderWidth,
    usePersistedAdminSiderWidth,
} from '@/components/admin-layout/usePersistedAdminSiderWidth';
import sidebarStyles from './protected-layout-sidebar.module.css';

const { Sider, Content } = Layout;
const { useBreakpoint } = Grid;

const EMPTY_PERMISSIONS: string[] = [];

type ProtectedShellMenuProps = {
    menuInlineCollapsed: boolean;
    openKeys: string[];
    setOpenKeys: React.Dispatch<React.SetStateAction<string[]>>;
    menuItems: MenuProps['items'];
    pathname: string | null;
    selectableRouteKeys: readonly string[];
    isMobile: boolean;
    setDrawerVisible: React.Dispatch<React.SetStateAction<boolean>>;
};

function ProtectedShellMenuFallback(props: ProtectedShellMenuProps & { menuTheme: 'light' | 'dark' }) {
    const { menuInlineCollapsed, openKeys, setOpenKeys, menuItems, pathname, selectableRouteKeys, isMobile, setDrawerVisible, menuTheme } =
        props;
    const selectedKeys = useMemo(
        () => resolveAdminMenuSelectedKeys(pathname, selectableRouteKeys),
        [pathname, selectableRouteKeys],
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
                if (isMobile) setDrawerVisible(false);
            }}
        />
    );
}

function ProtectedShellMenuWithSearch(props: ProtectedShellMenuProps & { menuTheme: 'light' | 'dark' }) {
    const searchParams = useSearchParams();
    const search = searchParams.toString();
    const { menuInlineCollapsed, openKeys, setOpenKeys, menuItems, pathname, selectableRouteKeys, isMobile, setDrawerVisible, menuTheme } =
        props;
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
                if (isMobile) setDrawerVisible(false);
            }}
        />
    );
}

export default function DashboardLayout({
    children,
}: {
    children: ReactNode;
}) {
    const [collapsed, setCollapsed] = useState(false);
    const [drawerVisible, setDrawerVisible] = useState(false);
    const screens = useBreakpoint();
    const pathname = usePathname();
    const router = useRouter();
    const { user, logout } = useAuth();
    const { t } = useI18n();
    const { effectiveTheme } = usePersonalization();
    const menuTheme = effectiveTheme;
    const permissions = user?.permissions ?? EMPTY_PERMISSIONS;
    const usePermissionFirst = permissions.length > 0;
    const { width: sidebarWidth, setWidth: setSidebarWidth } = usePersistedAdminSiderWidth();
    const {
        token: { colorBgContainer, borderRadiusLG },
    } = theme.useToken();

    const isMobile = !screens.md;

    useEffect(() => {
        if (isMobile) {
            setCollapsed(false);
        }
    }, [isMobile]);

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

    const menuItems = useMemo(
        () => filterSidebarMenuItems(allMenuItems, sidebarPermissionCtx) ?? [],
        [allMenuItems, sidebarPermissionCtx],
    );

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

    const userMenu: MenuProps = {
        items: [
            {
                key: 'profile',
                label: t(ADMIN_NAV_LABEL_KEYS.myProfile),
                icon: <UserOutlined />,
                onClick: () => router.push('/profile'),
            },
            {
                type: 'divider',
            },
            {
                key: 'logout',
                label: t(ADMIN_NAV_LABEL_KEYS.logout),
                icon: <LogoutOutlined />,
                onClick: () => logout(),
            },
        ],
    };

    const menuInlineCollapsed = !isMobile && collapsed;

    const SidebarContent = (
        <>
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
                {collapsed && !isMobile ? t('adminShell.branding.sidebarCompact') : t('adminShell.branding.sidebarExpanded')}
            </div>
            <Suspense
                fallback={
                    <ProtectedShellMenuFallback
                        menuTheme={menuTheme}
                        menuInlineCollapsed={menuInlineCollapsed}
                        openKeys={openKeys}
                        setOpenKeys={setOpenKeys}
                        menuItems={menuItems}
                        pathname={pathname}
                        selectableRouteKeys={selectableRouteKeys}
                        isMobile={isMobile}
                        setDrawerVisible={setDrawerVisible}
                    />
                }
            >
                <ProtectedShellMenuWithSearch
                    menuTheme={menuTheme}
                    menuInlineCollapsed={menuInlineCollapsed}
                    openKeys={openKeys}
                    setOpenKeys={setOpenKeys}
                    menuItems={menuItems}
                    pathname={pathname}
                    selectableRouteKeys={selectableRouteKeys}
                    isMobile={isMobile}
                    setDrawerVisible={setDrawerVisible}
                />
            </Suspense>
        </>
    );

    // AuthGate: wait for /me (see useAuth.isAuthInitializing). PermissionRouteGuard: ROUTE_PERMISSIONS vs user.permissions.
    return (
        <AuthGate mode="protected">
            <AppLayout>
            <CommandPaletteShell />
            <PermissionRouteGuard>
                <Suspense fallback={null}>
                    <PageLoader />
                </Suspense>
                <Layout style={{ minHeight: '100vh' }}>
                    {!isMobile && (
                        <Sider
                            trigger={null}
                            collapsible
                            collapsed={collapsed}
                            theme={menuTheme}
                            width={clampAdminSiderWidth(sidebarWidth)}
                            collapsedWidth={80}
                            className={sidebarStyles.siderRoot}
                        >
                            {!collapsed && (
                                <AdminDesktopSiderResizeHandle
                                    minWidth={ADMIN_SIDER_WIDTH_MIN}
                                    maxWidth={ADMIN_SIDER_WIDTH_MAX}
                                    onWidthChange={setSidebarWidth}
                                    ariaLabel={t('adminShell.aria.resizeSidebar')}
                                />
                            )}
                            {SidebarContent}
                        </Sider>
                    )}

                    <Drawer
                        title={t('adminShell.branding.drawerTitle')}
                        placement="left"
                        onClose={() => setDrawerVisible(false)}
                        open={drawerVisible}
                        styles={{ body: { padding: 0 } }}
                        width={250}
                    >
                        {SidebarContent}
                    </Drawer>

                    <Layout>
                        <AdminShellHeader
                            background={colorBgContainer}
                            isMobile={isMobile}
                            drawerVisible={drawerVisible}
                            collapsed={collapsed}
                            onToggleSidebar={() =>
                                isMobile ? setDrawerVisible((open) => !open) : setCollapsed(!collapsed)
                            }
                            userMenu={userMenu}
                            userLabel={
                                user?.firstName
                                    ? `${user.firstName} ${user.lastName}`
                                    : t('adminShell.branding.fallbackUserName')
                            }
                        />
                        <Content
                            style={{
                                margin: '24px 16px',
                                padding: 'var(--admin-density-padding, 24px)',
                                minHeight: 280,
                                background: colorBgContainer,
                                borderRadius: borderRadiusLG,
                                overflow: 'initial',
                            }}
                        >
                            <PasswordChangeRequiredRedirect />
                            <LicenseExpiryBanner />
                            <ImpersonationBanner />
                            <SuperAdminModeBanner />
                            <VerwaltungTenantContextGate />
                            <main id="main-content" tabIndex={-1}>
                                <SuperAdminTenantGate>{children}</SuperAdminTenantGate>
                            </main>
                        </Content>
                    </Layout>
                </Layout>
            </PermissionRouteGuard>
            </AppLayout>
        </AuthGate>
    );
}
