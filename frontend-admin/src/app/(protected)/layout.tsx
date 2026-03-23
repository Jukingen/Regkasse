'use client';

import React, { useState, useEffect, useMemo, useLayoutEffect, useCallback, useRef, ReactNode } from 'react';
import { Layout, Menu, Button, theme, Dropdown, Avatar, Drawer, Grid, MenuProps } from 'antd';
import {
    MenuFoldOutlined,
    MenuUnfoldOutlined,
    DashboardOutlined,
    FileDoneOutlined,
    ShoppingOutlined,
    FolderOutlined,
    GroupOutlined,
    UserOutlined,
    GiftOutlined,
    TagOutlined,
    FileSearchOutlined,
    SnippetsOutlined,
    EyeOutlined,
    SafetyCertificateOutlined,
    CreditCardOutlined,
    SafetyOutlined,
    CalendarOutlined,
    TeamOutlined,
    SettingOutlined,
    LogoutOutlined,
    ShopOutlined,
    AppstoreOutlined,
    UsergroupAddOutlined,
    ToolOutlined,
} from '@ant-design/icons';
import { usePathname } from 'next/navigation';
import Link from 'next/link';
// import { usePostApiAuthLogout } from '@/api/generated/auth/auth'; // Replaced by useAuth
import { AuthGate } from '@/shared/auth/AuthGate';
import { PermissionRouteGuard } from '@/shared/auth/PermissionRouteGuard';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import { canViewUsers, canShowRksvMenu } from '@/features/auth/constants/roles';
import { OPERATOR_VERIFICATIONS_COPY } from '@/shared/operatorTruthCopy';
import { buildRksvMenuGroups, getRksvOpenSubgroupKeys } from '@/shared/rksvMenuModel';
import { ADMIN_NAV_GROUP_LABELS, ADMIN_NAV_LABELS } from '@/shared/adminShellLabels';
import {
    ADMIN_SIDEBAR_GROUP_KEYS,
    collectSelectableRouteKeysFromMenuItems,
    filterSidebarMenuItems,
    getNonRksvSidebarOpenGroupKeys,
    resolveAdminMenuSelectedKeys,
    type SidebarPermissionContext,
} from '@/shared/adminSidebarNavigation';

const { Header, Sider, Content } = Layout;
const { useBreakpoint } = Grid;

const EMPTY_PERMISSIONS: string[] = [];

export default function DashboardLayout({
    children,
}: {
    children: ReactNode;
}) {
    const [collapsed, setCollapsed] = useState(false);
    const [drawerVisible, setDrawerVisible] = useState(false);
    // const [isLoadingAuth, setIsLoadingAuth] = useState(true); // Handled by AuthGuard
    const screens = useBreakpoint();
    const pathname = usePathname();
    const { user, logout } = useAuth();
    const permissions = user?.permissions ?? EMPTY_PERMISSIONS;
    const usePermissionFirst = permissions.length > 0;
    const {
        token: { colorBgContainer, borderRadiusLG },
    } = theme.useToken();

    // Responsive logic
    const isMobile = !screens.md; // md = 768px

    useEffect(() => {
        if (isMobile) {
            setCollapsed(false);
        }
    }, [isMobile]);

    const rksvGroups = useMemo(
        () => buildRksvMenuGroups(OPERATOR_VERIFICATIONS_COPY.navMenuLabel),
        [],
    );

    const rksvMenuSubtree = useMemo(
        () =>
            rksvGroups.map((g) => ({
                key: `rksv-grp-${g.id}`,
                label: g.groupLabel,
                children: g.items.map((item) => ({
                    key: item.key,
                    label: <Link href={item.href}>{item.label}</Link>,
                })),
            })),
        [rksvGroups],
    );

    const allMenuItems = useMemo(
        () => [
            { key: '/dashboard', icon: <DashboardOutlined />, label: <Link href="/dashboard">{ADMIN_NAV_LABELS.overview}</Link> },
            { key: '/invoices', icon: <FileDoneOutlined />, label: <Link href="/invoices">{ADMIN_NAV_LABELS.invoices}</Link> },
            {
                key: ADMIN_SIDEBAR_GROUP_KEYS.sortiment,
                icon: <AppstoreOutlined />,
                label: ADMIN_NAV_GROUP_LABELS.sortiment,
                children: [
                    { key: '/products', icon: <ShoppingOutlined />, label: <Link href="/products">{ADMIN_NAV_LABELS.products}</Link> },
                    { key: '/categories', icon: <FolderOutlined />, label: <Link href="/categories">{ADMIN_NAV_LABELS.categories}</Link> },
                    {
                        key: '/modifier-groups',
                        icon: <GroupOutlined />,
                        label: <Link href="/modifier-groups">{ADMIN_NAV_LABELS.modifierGroups}</Link>,
                    },
                ],
            },
            {
                key: ADMIN_SIDEBAR_GROUP_KEYS.kundenVorteile,
                icon: <UsergroupAddOutlined />,
                label: ADMIN_NAV_GROUP_LABELS.kundenVorteile,
                children: [
                    { key: '/customers', icon: <UserOutlined />, label: <Link href="/customers">{ADMIN_NAV_LABELS.customers}</Link> },
                    {
                        key: '/benefit-definitions',
                        icon: <GiftOutlined />,
                        label: <Link href="/benefit-definitions">{ADMIN_NAV_LABELS.benefitDefinitions}</Link>,
                    },
                    {
                        key: '/benefit-assignments',
                        icon: <TagOutlined />,
                        label: <Link href="/benefit-assignments">{ADMIN_NAV_LABELS.benefitAssignments}</Link>,
                    },
                ],
            },
            {
                key: ADMIN_SIDEBAR_GROUP_KEYS.kasseBelege,
                icon: <ShopOutlined />,
                label: ADMIN_NAV_GROUP_LABELS.kasseBelege,
                children: [
                    { key: '/receipts', icon: <FileSearchOutlined />, label: <Link href="/receipts">{ADMIN_NAV_LABELS.receipts}</Link> },
                    { key: '/payments', icon: <CreditCardOutlined />, label: <Link href="/payments">{ADMIN_NAV_LABELS.payments}</Link> },
                    {
                        key: '/tagesabschluss',
                        icon: <CalendarOutlined />,
                        label: <Link href="/tagesabschluss">{ADMIN_NAV_LABELS.tagesabschluss}</Link>,
                    },
                    {
                        key: '/receipt-templates',
                        icon: <SnippetsOutlined />,
                        label: <Link href="/receipt-templates">{ADMIN_NAV_LABELS.receiptTemplates}</Link>,
                    },
                    {
                        key: '/receipt-generate',
                        icon: <EyeOutlined />,
                        label: <Link href="/receipt-generate">{ADMIN_NAV_LABELS.receiptGenerate}</Link>,
                    },
                ],
            },
            { key: '/audit-logs', icon: <SafetyCertificateOutlined />, label: <Link href="/audit-logs">{ADMIN_NAV_LABELS.auditLogs}</Link> },
            {
                key: ADMIN_SIDEBAR_GROUP_KEYS.verwaltung,
                icon: <ToolOutlined />,
                label: ADMIN_NAV_GROUP_LABELS.verwaltung,
                children: [
                    { key: '/users', icon: <TeamOutlined />, label: <Link href="/users">{ADMIN_NAV_LABELS.users}</Link> },
                    { key: '/settings', icon: <SettingOutlined />, label: <Link href="/settings">{ADMIN_NAV_LABELS.settings}</Link> },
                ],
            },
            {
                key: '/rksv',
                icon: <SafetyOutlined />,
                label: ADMIN_NAV_GROUP_LABELS.rksv,
                children: rksvMenuSubtree,
            },
        ],
        [rksvMenuSubtree],
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

    const menuSelectedKeys = useMemo(
        () => resolveAdminMenuSelectedKeys(pathname, selectableRouteKeys),
        [pathname, selectableRouteKeys],
    );

    const [openKeys, setOpenKeys] = useState<string[]>([]);
    const rksvNavBootstrapped = useRef(false);

    const applyRksvOpenForPath = useCallback(
        (p: string) => {
            if (!p.startsWith('/rksv')) return;
            setOpenKeys((prev) => {
                const nonRksv = prev.filter((k) => k !== '/rksv' && !k.startsWith('rksv-grp-'));
                return [...nonRksv, '/rksv', ...getRksvOpenSubgroupKeys(p, rksvGroups)];
            });
        },
        [rksvGroups],
    );

    useLayoutEffect(() => {
        if (!canSeeRksv) return;
        const p = pathname ?? '';

        if (!rksvNavBootstrapped.current) {
            rksvNavBootstrapped.current = true;
            if (p.startsWith('/rksv')) {
                applyRksvOpenForPath(p);
            } else {
                setOpenKeys((prev) => (prev.includes('/rksv') ? prev : [...prev, '/rksv']));
            }
            return;
        }

        if (p.startsWith('/rksv')) {
            applyRksvOpenForPath(p);
        }
    }, [pathname, canSeeRksv, applyRksvOpenForPath]);

    useLayoutEffect(() => {
        const extra = getNonRksvSidebarOpenGroupKeys(pathname);
        if (extra.length === 0) return;
        setOpenKeys((prev) => {
            const next = new Set([...prev, ...extra]);
            return [...next];
        });
    }, [pathname]);

    const userMenu: MenuProps = {
        items: [
            {
                key: 'profile',
                label: ADMIN_NAV_LABELS.myProfile,
                icon: <UserOutlined />,
            },
            {
                type: 'divider',
            },
            {
                key: 'logout',
                label: ADMIN_NAV_LABELS.logout,
                icon: <LogoutOutlined />,
                onClick: () => logout(),
            },
        ],
    };

    const SidebarContent = (
        <>
            <div style={{ height: 64, margin: 16, background: 'rgba(0, 0, 0, 0.05)', borderRadius: 6, display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 'bold', overflow: 'hidden', whiteSpace: 'nowrap' }}>
                {collapsed ? 'RK' : 'REGKASSE ADMIN'}
            </div>
            <Menu
                theme="light"
                mode="inline"
                selectedKeys={menuSelectedKeys}
                openKeys={openKeys}
                onOpenChange={setOpenKeys}
                items={menuItems}
                onClick={() => {
                    if (isMobile) setDrawerVisible(false);
                }}
            />
        </>
    );

    return (
        <AuthGate mode="protected">
            <PermissionRouteGuard>
            <Layout style={{ minHeight: '100vh' }}>
                {!isMobile && (
                    <Sider trigger={null} collapsible collapsed={collapsed} theme="light" style={{ borderRight: '1px solid #f0f0f0' }}>
                        {SidebarContent}
                    </Sider>
                )}

                <Drawer
                    title="Regkasse Admin"
                    placement="left"
                    onClose={() => setDrawerVisible(false)}
                    open={drawerVisible}
                    styles={{ body: { padding: 0 } }}
                    width={250}
                >
                    {SidebarContent}
                </Drawer>

                <Layout>
                    <Header style={{ padding: 0, background: colorBgContainer, display: 'flex', justifyContent: 'space-between', alignItems: 'center', paddingRight: 24, zIndex: 1 }}>
                        <Button
                            type="text"
                            aria-label={
                                isMobile
                                    ? drawerVisible
                                        ? 'Navigationsmenü schließen'
                                        : 'Navigationsmenü öffnen'
                                    : collapsed
                                      ? 'Seitenleiste ausklappen'
                                      : 'Seitenleiste einklappen'
                            }
                            aria-expanded={isMobile ? drawerVisible : undefined}
                            icon={isMobile ? (drawerVisible ? <MenuFoldOutlined /> : <MenuUnfoldOutlined />) : (collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />)}
                            onClick={() => (isMobile ? setDrawerVisible((open) => !open) : setCollapsed(!collapsed))}
                            style={{
                                fontSize: '16px',
                                width: 64,
                                height: 64,
                            }}
                        />
                        <Dropdown menu={userMenu} placement="bottomRight">
                            <div style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 8 }}>
                                <Avatar icon={<UserOutlined />} />
                                {!isMobile && <span>{user?.firstName ? `${user.firstName} ${user.lastName}` : 'Admin User'}</span>}
                            </div>
                        </Dropdown>
                    </Header>
                    <Content
                        style={{
                            margin: '24px 16px',
                            padding: 24,
                            minHeight: 280,
                            background: colorBgContainer,
                            borderRadius: borderRadiusLG,
                            overflow: 'initial'
                        }}
                    >
                        <main id="main-content" tabIndex={-1}>
                            {children}
                        </main>
                    </Content>
                </Layout>
            </Layout>
            </PermissionRouteGuard>
        </AuthGate>
    );
}
