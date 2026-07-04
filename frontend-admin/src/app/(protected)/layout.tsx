'use client';

/**
 * Protected admin shell: sidebar via `AdminSidebar` (registry + permission filter).
 * Route access: `PermissionRouteGuard` + `ROUTE_PERMISSIONS`. Desktop Sider width: `usePersistedAdminSiderWidth`.
 */

import React, { Suspense, useState, useEffect, ReactNode } from 'react';
import { Layout, theme, Drawer, Grid } from 'antd';
import { AuthGate } from '@/shared/auth/AuthGate';
import { PermissionRouteGuard } from '@/shared/auth/PermissionRouteGuard';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useI18n } from '@/i18n';
import { usePersonalization } from '@/lib/personalization/PersonalizationProvider';
import { PageLoader } from '@/components/ui/PageLoader';
import { AdminSidebar } from '@/components/AdminSidebar';
import { ImpersonationBanner } from '@/components/admin-layout/ImpersonationBanner';
import { SuperAdminModeBanner } from '@/components/admin-layout/SuperAdminModeBanner';
import { SuperAdminTenantGate } from '@/components/admin-layout/SuperAdminTenantGate';
import { VerwaltungTenantContextGate } from '@/components/admin-layout/VerwaltungTenantContextGate';
import { AdminShellHeader } from '@/components/layout/Header';
import { CommandPaletteShell } from '@/components/CommandPalette';
import { AdminLayout } from '@/components/admin-layout/AdminLayout';
import { AppLayout } from '@/components/AppLayout';
import { LicenseExpiryBanner } from '@/components/admin-layout/LicenseExpiryBanner';
import { ReadOnlyBanner } from '@/components/ReadOnlyBanner';
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

export default function DashboardLayout({
    children,
}: {
    children: ReactNode;
}) {
    const [collapsed, setCollapsed] = useState(false);
    const [drawerVisible, setDrawerVisible] = useState(false);
    const screens = useBreakpoint();
    const { user, logout } = useAuth();
    const { t } = useI18n();
    const { effectiveTheme } = usePersonalization();
    const menuTheme = effectiveTheme;
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

    const menuInlineCollapsed = !isMobile && collapsed;

    const sidebarShell = (
        <AdminSidebar
            collapsed={collapsed}
            isMobile={isMobile}
            menuTheme={menuTheme}
            menuInlineCollapsed={menuInlineCollapsed}
            onNavigate={() => setDrawerVisible(false)}
        />
    );

    // AuthGate: wait for /me (see useAuth.isAuthInitializing). PermissionRouteGuard: ROUTE_PERMISSIONS vs user.permissions (content only).
    return (
        <AuthGate mode="protected">
            <AppLayout>
            <CommandPaletteShell />
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
                            {sidebarShell}
                        </Sider>
                    )}

                    <Drawer
                        title={t('adminShell.branding.drawerTitle')}
                        placement="left"
                        onClose={() => setDrawerVisible(false)}
                        open={drawerVisible}
                        styles={{ body: { padding: 0 } }}
                        size={250}
                    >
                        {sidebarShell}
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
                            user={user}
                            onLogout={() => void logout()}
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
                            <AdminLayout>
                                <LicenseExpiryBanner />
                                <ReadOnlyBanner />
                                <ImpersonationBanner />
                                <SuperAdminModeBanner />
                                <VerwaltungTenantContextGate />
                                <main id="main-content" tabIndex={-1}>
                                    <SuperAdminTenantGate>
                                        <PermissionRouteGuard>{children}</PermissionRouteGuard>
                                    </SuperAdminTenantGate>
                                </main>
                            </AdminLayout>
                        </Content>
                    </Layout>
                </Layout>
            </AppLayout>
        </AuthGate>
    );
}
