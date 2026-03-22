'use client';

import React, { useState, useEffect, useMemo, useLayoutEffect, useCallback, useRef, ReactNode } from 'react';
import { Layout, Menu, Button, theme, Dropdown, Avatar, Drawer, Grid, MenuProps } from 'antd';
import {
    MenuFoldOutlined,
    MenuUnfoldOutlined,
    DashboardOutlined,
    FileTextOutlined,
    UserOutlined,
    SettingOutlined,
    LogoutOutlined,
    SafetyCertificateOutlined,
    CreditCardOutlined,
    SafetyOutlined,
    CalendarOutlined,
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

const { Header, Sider, Content } = Layout;
const { useBreakpoint } = Grid;

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
    const permissions = user?.permissions ?? [];
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
            { key: '/dashboard', icon: <DashboardOutlined />, label: <Link href="/dashboard">Dashboard</Link> },
            { key: '/invoices', icon: <FileTextOutlined />, label: <Link href="/invoices">Invoices</Link> },
            { key: '/products', icon: <FileTextOutlined />, label: <Link href="/products">Products</Link> },
            { key: '/modifier-groups', icon: <FileTextOutlined />, label: <Link href="/modifier-groups">Add-on-Gruppen</Link> },
            { key: '/categories', icon: <FileTextOutlined />, label: <Link href="/categories">Categories</Link> },
            { key: '/customers', icon: <UserOutlined />, label: <Link href="/customers">Customers</Link> },
            { key: '/benefit-definitions', icon: <FileTextOutlined />, label: <Link href="/benefit-definitions">Vorteile (Definitionen)</Link> },
            { key: '/benefit-assignments', icon: <FileTextOutlined />, label: <Link href="/benefit-assignments">Vorteile (Zuweisungen)</Link> },
            { key: '/receipts', icon: <FileTextOutlined />, label: <Link href="/receipts">Receipts</Link> },
            { key: '/receipt-templates', icon: <FileTextOutlined />, label: <Link href="/receipt-templates">Receipt Templates</Link> },
            { key: '/receipt-generate', icon: <FileTextOutlined />, label: <Link href="/receipt-generate">Belegvorschau</Link> },
            { key: '/audit-logs', icon: <SafetyCertificateOutlined />, label: <Link href="/audit-logs">Audit Logs</Link> },
            { key: '/payments', icon: <CreditCardOutlined />, label: <Link href="/payments">Payments</Link> },
            {
                key: '/tagesabschluss',
                icon: <CalendarOutlined />,
                label: <Link href="/tagesabschluss">Tagesabschluss</Link>,
            },
            { key: '/users', icon: <UserOutlined />, label: <Link href="/users">Users</Link> },
            { key: '/settings', icon: <SettingOutlined />, label: <Link href="/settings">Settings</Link> },
            {
                key: '/rksv',
                icon: <SafetyOutlined />,
                label: 'RKSV',
                children: rksvMenuSubtree,
            },
        ],
        [rksvMenuSubtree],
    );

    const canSeeRksv = useMemo(() => {
        if (usePermissionFirst) return isMenuItemAllowed('/rksv', permissions);
        return canShowRksvMenu(user?.role ?? '');
    }, [usePermissionFirst, permissions, user?.role]);

    const menuItems = useMemo(() => {
        if (usePermissionFirst) {
            return allMenuItems.filter((item) => isMenuItemAllowed(item.key, permissions));
        }
        return allMenuItems.filter((item) => {
            if (item.key === '/users') return canViewUsers(user?.role ?? '');
            if (item.key === '/rksv') return canShowRksvMenu(user?.role ?? '');
            return true;
        });
    }, [allMenuItems, usePermissionFirst, permissions, user?.role]);

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

    const userMenu: MenuProps = {
        items: [
            {
                key: 'profile',
                label: 'My Profile',
                icon: <UserOutlined />,
            },
            {
                type: 'divider',
            },
            {
                key: 'logout',
                label: 'Logout',
                icon: <LogoutOutlined />,
                onClick: () => logout(),
            },
        ],
    };

    // Route is `/rksv`; menu child key is `/rksv/operations` so Ant Design can highlight the landing item.
    const menuSelectedKeys =
        pathname === '/rksv' ? ['/rksv/operations'] : pathname ? [pathname] : [];

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
                            icon={isMobile ? (drawerVisible ? <MenuFoldOutlined /> : <MenuUnfoldOutlined />) : (collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />)}
                            onClick={() => isMobile ? setDrawerVisible(true) : setCollapsed(!collapsed)}
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
                        {children}
                    </Content>
                </Layout>
            </Layout>
            </PermissionRouteGuard>
        </AuthGate>
    );
}
