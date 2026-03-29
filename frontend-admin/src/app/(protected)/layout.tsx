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
    InboxOutlined,
    UsergroupAddOutlined,
    ToolOutlined,
    ControlOutlined,
    FundOutlined,
    LineChartOutlined,
    TableOutlined,
    CloudServerOutlined,
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
import { ADMIN_NAV_GROUP_LABEL_KEYS, ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import {
    ADMIN_SIDEBAR_GROUP_KEYS,
    collectSelectableRouteKeysFromMenuItems,
    filterSidebarMenuItems,
    getNonRksvSidebarOpenGroupKeys,
    resolveAdminMenuSelectedKeys,
    type SidebarPermissionContext,
} from '@/shared/adminSidebarNavigation';
import { HeaderLanguageQuickSwitch } from '@/components/admin-layout/HeaderLanguageQuickSwitch';

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
    const { t } = useI18n();
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
            { key: '/dashboard', icon: <DashboardOutlined />, label: <Link href="/dashboard">{t(ADMIN_NAV_LABEL_KEYS.overview)}</Link> },
            { key: '/invoices', icon: <FileDoneOutlined />, label: <Link href="/invoices">{t(ADMIN_NAV_LABEL_KEYS.invoices)}</Link> },
            {
                key: ADMIN_SIDEBAR_GROUP_KEYS.sortiment,
                icon: <AppstoreOutlined />,
                label: t(ADMIN_NAV_GROUP_LABEL_KEYS.sortiment),
                children: [
                    { key: '/products', icon: <ShoppingOutlined />, label: <Link href="/products">{t(ADMIN_NAV_LABEL_KEYS.products)}</Link> },
                    { key: '/pricing-rules', icon: <TagOutlined />, label: <Link href="/pricing-rules">{t(ADMIN_NAV_LABEL_KEYS.pricingRules)}</Link> },
                    { key: '/categories', icon: <FolderOutlined />, label: <Link href="/categories">{t(ADMIN_NAV_LABEL_KEYS.categories)}</Link> },
                    {
                        key: '/modifier-groups',
                        icon: <GroupOutlined />,
                        label: <Link href="/modifier-groups">{t(ADMIN_NAV_LABEL_KEYS.modifierGroups)}</Link>,
                    },
                    { key: '/inventory', icon: <InboxOutlined />, label: <Link href="/inventory">{t(ADMIN_NAV_LABEL_KEYS.inventory)}</Link> },
                ],
            },
            {
                key: ADMIN_SIDEBAR_GROUP_KEYS.kundenVorteile,
                icon: <UsergroupAddOutlined />,
                label: t(ADMIN_NAV_GROUP_LABEL_KEYS.kundenVorteile),
                children: [
                    { key: '/customers', icon: <UserOutlined />, label: <Link href="/customers">{t(ADMIN_NAV_LABEL_KEYS.customers)}</Link> },
                    {
                        key: '/benefit-definitions',
                        icon: <GiftOutlined />,
                        label: <Link href="/benefit-definitions">{t(ADMIN_NAV_LABEL_KEYS.benefitDefinitions)}</Link>,
                    },
                    {
                        key: '/benefit-assignments',
                        icon: <TagOutlined />,
                        label: <Link href="/benefit-assignments">{t(ADMIN_NAV_LABEL_KEYS.benefitAssignments)}</Link>,
                    },
                ],
            },
            {
                key: ADMIN_SIDEBAR_GROUP_KEYS.kasseBelege,
                icon: <ShopOutlined />,
                label: t(ADMIN_NAV_GROUP_LABEL_KEYS.kasseBelege),
                children: [
                    {
                        key: '/operations-center',
                        icon: <ControlOutlined />,
                        label: <Link href="/operations-center">{t(ADMIN_NAV_LABEL_KEYS.operationsCenter)}</Link>,
                    },
                    {
                        key: '/reporting',
                        icon: <FundOutlined />,
                        label: <Link href="/reporting">{t(ADMIN_NAV_LABEL_KEYS.reporting)}</Link>,
                    },
                    {
                        key: '/reporting/report-center',
                        icon: <FundOutlined />,
                        label: <Link href="/reporting/report-center">{t(ADMIN_NAV_LABEL_KEYS.reportCenter)}</Link>,
                    },
                    {
                        key: '/reporting/staff',
                        icon: <TeamOutlined />,
                        label: <Link href="/reporting/staff">{t(ADMIN_NAV_LABEL_KEYS.staffPerformance)}</Link>,
                    },
                    {
                        key: '/reporting/tagesbericht',
                        icon: <FileDoneOutlined />,
                        label: <Link href="/reporting/tagesbericht">{t(ADMIN_NAV_LABEL_KEYS.tagesbericht)}</Link>,
                    },
                    {
                        key: '/reporting/monatsbericht',
                        icon: <LineChartOutlined />,
                        label: <Link href="/reporting/monatsbericht">{t(ADMIN_NAV_LABEL_KEYS.monatsbericht)}</Link>,
                    },
                    {
                        key: '/reporting/jahresbericht',
                        icon: <LineChartOutlined />,
                        label: <Link href="/reporting/jahresbericht">{t(ADMIN_NAV_LABEL_KEYS.jahresbericht)}</Link>,
                    },
                    { key: '/receipts', icon: <FileSearchOutlined />, label: <Link href="/receipts">{t(ADMIN_NAV_LABEL_KEYS.receipts)}</Link> },
                    { key: '/payments', icon: <CreditCardOutlined />, label: <Link href="/payments">{t(ADMIN_NAV_LABEL_KEYS.payments)}</Link> },
                    {
                        key: '/tagesabschluss',
                        icon: <CalendarOutlined />,
                        label: <Link href="/tagesabschluss">{t(ADMIN_NAV_LABEL_KEYS.tagesabschluss)}</Link>,
                    },
                    {
                        key: '/receipt-templates',
                        icon: <SnippetsOutlined />,
                        label: <Link href="/receipt-templates">{t(ADMIN_NAV_LABEL_KEYS.receiptTemplates)}</Link>,
                    },
                    {
                        key: '/receipt-generate',
                        icon: <EyeOutlined />,
                        label: <Link href="/receipt-generate">{t(ADMIN_NAV_LABEL_KEYS.receiptGenerate)}</Link>,
                    },
                    {
                        key: '/tables',
                        icon: <TableOutlined />,
                        label: <Link href="/tables">{t(ADMIN_NAV_LABEL_KEYS.tables)}</Link>,
                    },
                ],
            },
            { key: '/audit-logs', icon: <SafetyCertificateOutlined />, label: <Link href="/audit-logs">{t(ADMIN_NAV_LABEL_KEYS.auditLogs)}</Link> },
            {
                key: ADMIN_SIDEBAR_GROUP_KEYS.verwaltung,
                icon: <ToolOutlined />,
                label: t(ADMIN_NAV_GROUP_LABEL_KEYS.verwaltung),
                children: [
                    { key: '/users', icon: <TeamOutlined />, label: <Link href="/users">{t(ADMIN_NAV_LABEL_KEYS.users)}</Link> },
                    {
                        key: ADMIN_SIDEBAR_GROUP_KEYS.settingsArea,
                        icon: <SettingOutlined />,
                        label: t(ADMIN_NAV_LABEL_KEYS.settingsHub),
                        children: [
                            {
                                key: '/settings',
                                icon: <ShopOutlined />,
                                label: <Link href="/settings">{t(ADMIN_NAV_LABEL_KEYS.companySettings)}</Link>,
                            },
                            {
                                key: '/settings/payment-methods',
                                icon: <CreditCardOutlined />,
                                label: (
                                    <Link href="/settings/payment-methods">{t(ADMIN_NAV_LABEL_KEYS.paymentMethods)}</Link>
                                ),
                            },
                            {
                                key: '/settings/backup-dr',
                                icon: <CloudServerOutlined />,
                                label: (
                                    <Link href="/settings/backup-dr">{t(ADMIN_NAV_LABEL_KEYS.backupDr)}</Link>
                                ),
                            },
                        ],
                    },
                ],
            },
            {
                key: '/rksv',
                icon: <SafetyOutlined />,
                label: t(ADMIN_NAV_GROUP_LABEL_KEYS.rksv),
                children: rksvMenuSubtree,
            },
        ],
        [rksvMenuSubtree, t],
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
            return Array.from(next);
        });
    }, [pathname]);

    const userMenu: MenuProps = {
        items: [
            {
                key: 'profile',
                label: t(ADMIN_NAV_LABEL_KEYS.myProfile),
                icon: <UserOutlined />,
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

    const SidebarContent = (
        <>
            <div style={{ height: 64, margin: 16, background: 'rgba(0, 0, 0, 0.05)', borderRadius: 6, display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 'bold', overflow: 'hidden', whiteSpace: 'nowrap' }}>
                {collapsed ? t('adminShell.branding.sidebarCompact') : t('adminShell.branding.sidebarExpanded')}
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
                    <Header style={{ padding: 0, background: colorBgContainer, display: 'flex', justifyContent: 'space-between', alignItems: 'center', paddingRight: 24, zIndex: 1 }}>
                        <Button
                            type="text"
                            aria-label={
                                isMobile
                                    ? drawerVisible
                                        ? t('adminShell.aria.closeNav')
                                        : t('adminShell.aria.openNav')
                                    : collapsed
                                      ? t('adminShell.aria.expandSidebar')
                                      : t('adminShell.aria.collapseSidebar')
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
                        <div
                            style={{ display: 'flex', alignItems: 'center', gap: 12, marginLeft: 'auto' }}
                            role="toolbar"
                            aria-label={t('adminShell.header.headerActionsAria')}
                        >
                            <HeaderLanguageQuickSwitch />
                            <Dropdown menu={userMenu} placement="bottomRight">
                                <div style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 8 }}>
                                    <Avatar icon={<UserOutlined />} />
                                    {!isMobile && <span>{user?.firstName ? `${user.firstName} ${user.lastName}` : t('adminShell.branding.fallbackUserName')}</span>}
                                </div>
                            </Dropdown>
                        </div>
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
