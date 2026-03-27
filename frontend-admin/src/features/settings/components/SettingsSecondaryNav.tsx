'use client';

// Horizontal sub-navigation for /settings routes; labels align with the sidebar.

import React, { useMemo } from 'react';
import { Menu } from 'antd';
import type { MenuProps } from 'antd';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { ShopOutlined, CreditCardOutlined } from '@ant-design/icons';
import { useI18n } from '@/i18n/I18nProvider';
import { ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';

export function SettingsSecondaryNav() {
  const pathname = usePathname() ?? '';
  const { t } = useI18n();

  const items: MenuProps['items'] = useMemo(
    () => [
      {
        key: '/settings',
        icon: <ShopOutlined />,
        label: (
          <Link href="/settings" prefetch={false}>
            {t(ADMIN_NAV_LABEL_KEYS.companySettings)}
          </Link>
        ),
      },
      {
        key: '/settings/payment-methods',
        icon: <CreditCardOutlined />,
        label: (
          <Link href="/settings/payment-methods" prefetch={false}>
            {t(ADMIN_NAV_LABEL_KEYS.paymentMethods)}
          </Link>
        ),
      },
    ],
    [t],
  );

  const selectedKeys = useMemo(() => {
    if (pathname === '/settings' || pathname.startsWith('/settings/')) {
      if (pathname.startsWith('/settings/payment-methods')) return ['/settings/payment-methods'];
      if (pathname === '/settings') return ['/settings'];
    }
    return [pathname];
  }, [pathname]);

  return (
    <Menu
      mode="horizontal"
      selectedKeys={selectedKeys}
      items={items}
      style={{ borderBottom: '1px solid #f0f0f0', marginBottom: 0 }}
    />
  );
}
