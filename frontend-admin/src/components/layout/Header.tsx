'use client';



import { Button, Layout } from 'antd';

import { MenuFoldOutlined, MenuUnfoldOutlined } from '@ant-design/icons';

import Image from 'next/image';

import Link from 'next/link';

import { useI18n } from '@/i18n';



import { HeaderTenantSection } from '@/features/auth/components/HeaderTenantSection';

import { HeaderLanguageQuickSwitch } from '@/components/admin-layout/HeaderLanguageQuickSwitch';

import { HeaderThemeQuickSwitch } from '@/components/admin-layout/HeaderThemeQuickSwitch';

import { CashRegisterQuickSwitch } from '@/components/layout/CashRegisterQuickSwitch';

import { EnvironmentBadge } from '@/components/EnvironmentBadge';

import { LicenseStatusIndicator } from '@/components/admin-layout/LicenseStatusIndicator';

import { ActivityNotificationsBell } from '@/features/activity-notifications/components/ActivityNotificationsBell';

import { AlertBell } from '@/components/AlertBell';

import { MonatsbelegGlobalBadge } from '@/features/dashboard/components/MonatsbelegGlobalBadge';

import { AdminHeaderUserMenu } from '@/components/layout/AdminHeaderUserMenu';
import { GlobalSearch } from '@/components/admin-layout/GlobalSearch';

import type { AuthUser } from '@/shared/auth/types';



const { Header: AntHeader } = Layout;



export type AdminShellHeaderProps = {

    background: string;

    isMobile: boolean;

    drawerVisible: boolean;

    collapsed: boolean;

    onToggleSidebar: () => void;

    user: AuthUser | null | undefined;

    onLogout: () => void;

};



/** Protected shell top bar: nav toggle, brand, tenant context, license, account menu. */

export function AdminShellHeader({

    background,

    isMobile,

    drawerVisible,

    collapsed,

    onToggleSidebar,

    user,

    onLogout,

}: AdminShellHeaderProps) {

    const { t } = useI18n();



    const navAriaLabel = isMobile

        ? drawerVisible

            ? t('adminShell.aria.closeNav')

            : t('adminShell.aria.openNav')

        : collapsed

          ? t('adminShell.aria.expandSidebar')

          : t('adminShell.aria.collapseSidebar');



    const navIcon = isMobile ? (

        drawerVisible ? (

            <MenuFoldOutlined />

        ) : (

            <MenuUnfoldOutlined />

        )

    ) : collapsed ? (

        <MenuUnfoldOutlined />

    ) : (

        <MenuFoldOutlined />

    );



    return (

        <AntHeader

            className="admin-shell-header"

            style={{ ['--header-bg' as string]: background, ['--admin-header-bg' as string]: background }}

        >

            <header className="admin-header" data-compact={isMobile || undefined}>

                <div className="admin-header-left">

                    <Button

                        type="text"

                        className="admin-header-nav-toggle"

                        aria-label={navAriaLabel}

                        aria-expanded={isMobile ? drawerVisible : undefined}

                        icon={navIcon}

                        onClick={onToggleSidebar}

                    />

                    <Link

                        href="/dashboard"

                        className="admin-header-logo"

                        aria-label={t('adminShell.branding.sidebarExpanded')}

                    >

                        <Image

                            src="/logo.svg"

                            alt=""

                            width={32}

                            height={32}

                            className="admin-header-logo-image"

                            priority

                        />

                        <span className="admin-header-logo-text">Regkasse</span>

                    </Link>

                </div>



                <div className="admin-header-center" aria-hidden="true" />



                <div

                    className="admin-header-right"

                    role="toolbar"

                    aria-label={t('adminShell.header.headerActionsAria')}

                >

                    <div className="admin-header-toolbar-scroll">

                        <span className="admin-header-env-badge">

                            <EnvironmentBadge />

                        </span>



                        <HeaderTenantSection isMobile={isMobile} />



                        <LicenseStatusIndicator compact={isMobile} />



                        <div className="admin-header-utilities">

                            <CashRegisterQuickSwitch isMobile={isMobile} />

                            <HeaderThemeQuickSwitch />

                            <HeaderLanguageQuickSwitch />

                            <AlertBell />

                            <ActivityNotificationsBell />

                            <MonatsbelegGlobalBadge />

                        </div>

                    </div>



                    <GlobalSearch isMobile={isMobile} />

                    <AdminHeaderUserMenu

                        user={user}

                        fallbackLabel={t('adminShell.branding.fallbackUserName')}

                        isMobile={isMobile}

                        onLogout={onLogout}

                    />

                </div>

            </header>

        </AntHeader>

    );

}

