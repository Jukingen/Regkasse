'use client';

/**
 * Horizontal sub-navigation for the settings hub. Route paths are owned by `SETTINGS_AREA_ROUTE_PATHS`
 * (`shared/settingsAreaRoutes.ts`); labels use the same i18n keys as the main sidebar.
 * Tabs are filtered by permission — unauthorized settings pages are hidden.
 */

import React, { useMemo, type ComponentType } from 'react';
import { Menu } from 'antd';
import type { MenuProps } from 'antd';
import Link from 'next/link';
import { usePathname, useSearchParams } from 'next/navigation';
import {
    ShopOutlined,
    BgColorsOutlined,
    CreditCardOutlined,
    ExperimentOutlined,
    ClockCircleOutlined,
    LaptopOutlined,
    DisconnectOutlined,
    SafetyCertificateOutlined,
    BankOutlined,
    SettingOutlined,
    CalendarOutlined,
} from '@ant-design/icons';
import { useI18n } from '@/i18n/I18nProvider';
import { ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import {
    SETTINGS_AREA_ROUTE_PATHS,
    type SettingsAreaRoutePath,
} from '@/shared/settingsAreaRoutes';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';

const SETTINGS_TAB_META: Record<
    SettingsAreaRoutePath,
    { labelKey: string; Icon: ComponentType }
> = {
    '/settings': { labelKey: ADMIN_NAV_LABEL_KEYS.settingsHub, Icon: SettingOutlined },
    '/settings/company': { labelKey: ADMIN_NAV_LABEL_KEYS.companySettings, Icon: ShopOutlined },
    '/settings/working-hours': {
        labelKey: ADMIN_NAV_LABEL_KEYS.workingHours,
        Icon: CalendarOutlined,
    },
    '/settings/tse': { labelKey: 'settings.tabs.tse', Icon: SafetyCertificateOutlined },
    '/settings/finanzonline': { labelKey: 'settings.tabs.finanzOnline', Icon: BankOutlined },
    '/settings/session': { labelKey: ADMIN_NAV_LABEL_KEYS.sessionSettings, Icon: ClockCircleOutlined },
    '/settings/sessions': { labelKey: ADMIN_NAV_LABEL_KEYS.activeSessions, Icon: LaptopOutlined },
    '/settings/offline': { labelKey: ADMIN_NAV_LABEL_KEYS.offlineSettings, Icon: DisconnectOutlined },
    '/settings/personalization': { labelKey: ADMIN_NAV_LABEL_KEYS.personalization, Icon: BgColorsOutlined },
    '/settings/payment-methods': { labelKey: ADMIN_NAV_LABEL_KEYS.paymentMethods, Icon: CreditCardOutlined },
    '/settings/payment': { labelKey: ADMIN_NAV_LABEL_KEYS.paymentGateway, Icon: CreditCardOutlined },
    '/settings/development-mode': { labelKey: ADMIN_NAV_LABEL_KEYS.developmentMode, Icon: ExperimentOutlined },
};

export function SettingsSecondaryNav() {
  const pathname = usePathname() ?? '';
  const searchParams = useSearchParams();
  const { t } = useI18n();
  const { user } = useAuth();
  const permissions = user?.permissions ?? [];

  const visiblePaths = useMemo(
    () => SETTINGS_AREA_ROUTE_PATHS.filter((path) => isMenuItemAllowed(path, permissions)),
    [permissions],
  );

  const items: MenuProps['items'] = useMemo(
    () =>
      visiblePaths.map((path) => {
        const { labelKey, Icon } = SETTINGS_TAB_META[path];
        return {
          key: path,
          icon: <Icon />,
          label: (
            <Link href={path === '/settings' ? '/settings' : path} prefetch={false}>
              {t(labelKey)}
            </Link>
          ),
        };
      }),
    [t, visiblePaths],
  );

  const selectedKeys = useMemo(() => {
    if (!pathname.startsWith('/settings')) return [pathname];

    const hubTab = searchParams?.get('tab')?.trim().toLowerCase();
    if ((pathname === '/settings' || pathname === '/settings/') && hubTab) {
      const tabRoute = `/settings/${hubTab}` as SettingsAreaRoutePath;
      if (visiblePaths.includes(tabRoute)) {
        return [tabRoute];
      }
    }

    const sorted = [...visiblePaths].sort((a, b) => b.length - a.length);
    for (const route of sorted) {
      if (pathname === route || pathname.startsWith(`${route}/`)) return [route];
    }
    if (pathname === '/settings' || pathname === '/settings/') {
      if (visiblePaths.includes('/settings')) {
        return ['/settings'];
      }
      return visiblePaths[0] ? [visiblePaths[0]] : [];
    }
    return visiblePaths[0] ? [visiblePaths[0]] : [];
  }, [pathname, searchParams, visiblePaths]);

  if (visiblePaths.length === 0) {
    return null;
  }

  return (
    <Menu
      mode="horizontal"
      selectedKeys={selectedKeys}
      items={items}
      style={{ borderBottom: '1px solid #f0f0f0', marginBottom: 0 }}
    />
  );
}
