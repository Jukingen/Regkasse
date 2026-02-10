'use client';

import React, { useState, useEffect, ReactNode } from 'react';
import { Layout, Menu, Button, theme, Dropdown, Avatar, Drawer, Grid, Spin, MenuProps } from 'antd';
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
} from '@ant-design/icons';
import { useRouter, usePathname } from 'next/navigation';
import Link from 'next/link';
// import { usePostApiAuthLogout } from '@/api/generated/auth/auth'; // Replaced by useAuth
import { AuthGate } from '@/shared/auth/AuthGate';
import { useAuth } from '@/features/auth/hooks/useAuth';

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
    const router = useRouter();
    const pathname = usePathname();
    const { user, logout } = useAuth(); // Destructure only what is needed for UI
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

    const menuItems = [
        {
            key: '/dashboard',
            icon: <DashboardOutlined />,
            label: <Link href="/dashboard">Dashboard</Link>,
        },
        {
            key: '/invoices',
            icon: <FileTextOutlined />,
            label: <Link href="/invoices">Invoices</Link>,
        },
        {
            key: '/audit-logs',
            icon: <SafetyCertificateOutlined />,
            label: <Link href="/audit-logs">Audit Logs</Link>,
        },
        {
            key: '/payments',
            icon: <CreditCardOutlined />,
            label: <Link href="/payments">Payments</Link>,
        },
        {
            key: '/users',
            icon: <UserOutlined />,
            label: <Link href="/users">Users</Link>,
        },
        {
            key: '/settings',
            icon: <SettingOutlined />,
            label: <Link href="/settings">Settings</Link>,
        }
    ];

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

    const SidebarContent = (
        <>
            <div style={{ height: 64, margin: 16, background: 'rgba(0, 0, 0, 0.05)', borderRadius: 6, display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 'bold', overflow: 'hidden', whiteSpace: 'nowrap' }}>
                {collapsed ? 'RK' : 'REGKASSE ADMIN'}
            </div>
            <Menu
                theme="light"
                mode="inline"
                selectedKeys={[pathname]}
                items={menuItems}
                onClick={() => {
                    if (isMobile) setDrawerVisible(false);
                }}
            />
        </>
    );

    return (
        <AuthGate mode="protected">
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
        </AuthGate>
    );
}
