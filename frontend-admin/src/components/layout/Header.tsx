'use client';

import { Avatar, Button, Dropdown, Layout } from 'antd';
import type { MenuProps } from 'antd';
import { MenuFoldOutlined, MenuUnfoldOutlined, UserOutlined } from '@ant-design/icons';
import type { ReactNode } from 'react';

import { HeaderDevTenantSwitch } from '@/features/auth/components/HeaderDevTenantSwitch';
import { HeaderLanguageQuickSwitch } from '@/components/admin-layout/HeaderLanguageQuickSwitch';
import { TenantBadge } from '@/components/admin-layout/TenantBadge';
import { EnvironmentBadge } from '@/components/EnvironmentBadge';
import { LicenseStatusBadge } from '@/components/LicenseStatusBadge';
import { MonatsbelegGlobalBadge } from '@/features/dashboard/components/MonatsbelegGlobalBadge';
import { useI18n } from '@/i18n';

const { Header: AntHeader } = Layout;

export type AdminShellHeaderProps = {
    background: string;
    isMobile: boolean;
    drawerVisible: boolean;
    collapsed: boolean;
    onToggleSidebar: () => void;
    userMenu: MenuProps;
    userLabel: ReactNode;
};

/** Protected shell top bar: nav toggle + tenant badge + dev switcher + account menu. */
export function AdminShellHeader({
    background,
    isMobile,
    drawerVisible,
    collapsed,
    onToggleSidebar,
    userMenu,
    userLabel,
}: AdminShellHeaderProps) {
    const { t } = useI18n();

    return (
        <AntHeader
            style={{
                padding: 0,
                background,
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                paddingRight: 24,
                zIndex: 1,
            }}
        >
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
                icon={
                    isMobile ? (
                        drawerVisible ? (
                            <MenuFoldOutlined />
                        ) : (
                            <MenuUnfoldOutlined />
                        )
                    ) : collapsed ? (
                        <MenuUnfoldOutlined />
                    ) : (
                        <MenuFoldOutlined />
                    )
                }
                onClick={onToggleSidebar}
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
                <EnvironmentBadge />
                <TenantBadge />
                <HeaderDevTenantSwitch />
                <LicenseStatusBadge />
                <HeaderLanguageQuickSwitch />
                <MonatsbelegGlobalBadge />
                <Dropdown menu={userMenu} placement="bottomRight">
                    <div style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 8 }}>
                        <Avatar icon={<UserOutlined />} />
                        {!isMobile ? <span>{userLabel}</span> : null}
                    </div>
                </Dropdown>
            </div>
        </AntHeader>
    );
}
