'use client';

import { MenuFoldOutlined, MenuUnfoldOutlined, QuestionCircleOutlined } from '@ant-design/icons';
import { Button, Layout } from 'antd';
import Link from 'next/link';

import { AlertBell } from '@/components/AlertBell';
import { EnvironmentBadge } from '@/components/EnvironmentBadge';
import { openKeyboardShortcutsHelp } from '@/components/KeyboardShortcutsHelp';
import { OptimizedImage } from '@/components/OptimizedImage';
import { SearchBar } from '@/components/SearchBar';
import { HeaderLanguageQuickSwitch } from '@/components/admin-layout/HeaderLanguageQuickSwitch';
import { HeaderThemeQuickSwitch } from '@/components/admin-layout/HeaderThemeQuickSwitch';
import { LicenseStatusIndicator } from '@/components/admin-layout/LicenseStatusIndicator';
import { AdminHeaderUserMenu } from '@/components/layout/AdminHeaderUserMenu';
import { CashRegisterQuickSwitch } from '@/components/layout/CashRegisterQuickSwitch';
import { ActivityNotificationsBell } from '@/features/activity-notifications/components/ActivityNotificationsBell';
import { HeaderTenantSection } from '@/features/auth/components/HeaderTenantSection';
import { MonatsbelegGlobalBadge } from '@/features/dashboard/components/MonatsbelegGlobalBadge';
import { useI18n } from '@/i18n';
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
            <OptimizedImage
              src="/logo.svg"
              alt=""
              width={32}
              height={32}
              className="admin-header-logo-image"
              priority
              loading="eager"
            />
            <span className="admin-header-logo-text">Regkasse</span>
          </Link>

          {/* Global selected cash register (name, status, quick switch) */}
          <div
            className="admin-header-register-context"
            data-testid="admin-header-register-context"
          >
            <CashRegisterQuickSwitch isMobile={isMobile} />
          </div>
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
              <HeaderThemeQuickSwitch />

              <HeaderLanguageQuickSwitch />

              <Button
                type="default"
                size="small"
                icon={<QuestionCircleOutlined />}
                className="admin-header-tool-btn"
                onClick={() => openKeyboardShortcutsHelp()}
                aria-label={t('keyboardShortcuts.help')}
                title={t('keyboardShortcuts.help')}
              />

              <AlertBell />

              <ActivityNotificationsBell />

              <MonatsbelegGlobalBadge />
            </div>
          </div>

          <SearchBar isMobile={isMobile} />

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
