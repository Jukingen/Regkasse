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
import { usePathname } from 'next/navigation';
import {
    ShopOutlined,
    BgColorsOutlined,
    CreditCardOutlined,
    CloudServerOutlined,
    ExperimentOutlined,
    ClockCircleOutlined,
    DisconnectOutlined,
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
    '/settings/company': { labelKey: ADMIN_NAV_LABEL_KEYS.companySettings, Icon: ShopOutlined },
    '/settings/session': { labelKey: ADMIN_NAV_LABEL_KEYS.sessionSettings, Icon: ClockCircleOutlined },
    '/settings/offline': { labelKey: ADMIN_NAV_LABEL_KEYS.offlineSettings, Icon: DisconnectOutlined },
    '/settings/personalization': { labelKey: ADMIN_NAV_LABEL_KEYS.personalization, Icon: BgColorsOutlined },
    '/settings/payment-methods': { labelKey: ADMIN_NAV_LABEL_KEYS.paymentMethods, Icon: CreditCardOutlined },
    '/settings/backup-dr': { labelKey: ADMIN_NAV_LABEL_KEYS.backupDr, Icon: CloudServerOutlined },
    '/settings/development-mode': { labelKey: ADMIN_NAV_LABEL_KEYS.developmentMode, Icon: ExperimentOutlined },
};

export function SettingsSecondaryNav() {
  const pathname = usePathname() ?? '';
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
            <Link href={path} prefetch={false}>
              {t(labelKey)}
            </Link>
          ),
        };
      }),
    [t, visiblePaths],
  );

  const selectedKeys = useMemo(() => {
    if (!pathname.startsWith('/settings')) return [pathname];
    const sorted = [...visiblePaths].sort((a, b) => b.length - a.length);
    for (const route of sorted) {
      if (pathname === route || pathname.startsWith(`${route}/`)) return [route];
    }
    return visiblePaths[0] ? [visiblePaths[0]] : [];
  }, [pathname, visiblePaths]);

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
