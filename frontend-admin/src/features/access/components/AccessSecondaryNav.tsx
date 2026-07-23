'use client';

import {
  AppstoreOutlined,
  AuditOutlined,
  CloudUploadOutlined,
  HistoryOutlined,
  KeyOutlined,
  PieChartOutlined,
  SafetyOutlined,
  TeamOutlined,
} from '@ant-design/icons';
import type { MenuProps } from 'antd';
import { Menu } from 'antd';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import React, { type ComponentType, useMemo } from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';
import { useI18n } from '@/i18n/I18nProvider';
import { ACCESS_AREA_ROUTE_PATHS, type AccessAreaRoutePath } from '@/shared/accessAreaRoutes';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';

const ACCESS_TAB_META: Record<AccessAreaRoutePath, { labelKey: string; Icon: ComponentType }> = {
  '/admin/access': { labelKey: 'nav.accessOverview', Icon: KeyOutlined },
  '/admin/users': { labelKey: 'nav.users', Icon: TeamOutlined },
  '/admin/access/roles': { labelKey: 'nav.rolesPermissions', Icon: SafetyOutlined },
  '/admin/access/matrix': { labelKey: 'nav.accessMatrix', Icon: AuditOutlined },
  '/admin/access/permission-history': { labelKey: 'nav.permissionHistory', Icon: HistoryOutlined },
  '/admin/access/permission-requests': { labelKey: 'nav.permissionRequests', Icon: AuditOutlined },
  '/admin/access/permission-packages': { labelKey: 'nav.permissionPackages', Icon: AppstoreOutlined },
  '/admin/access/permission-backups': { labelKey: 'nav.permissionBackups', Icon: CloudUploadOutlined },
  '/admin/access/permission-stats': { labelKey: 'nav.permissionStats', Icon: PieChartOutlined },
};

export function AccessSecondaryNav() {
  const pathname = usePathname() ?? '';
  const { t } = useI18n();
  const { user } = useAuth();
  const permissions = user?.permissions ?? [];

  const visiblePaths = useMemo(
    () => ACCESS_AREA_ROUTE_PATHS.filter((path) => isMenuItemAllowed(path, permissions)),
    [permissions]
  );

  const items: MenuProps['items'] = useMemo(
    () =>
      visiblePaths.map((path) => {
        const { labelKey, Icon } = ACCESS_TAB_META[path];
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
    [t, visiblePaths]
  );

  const selectedKeys = useMemo(() => {
    const sorted = [...visiblePaths].sort((a, b) => b.length - a.length);
    for (const route of sorted) {
      if (pathname === route || pathname.startsWith(`${route}/`)) return [route];
    }
    return visiblePaths[0] ? [visiblePaths[0]] : [];
  }, [pathname, visiblePaths]);

  if (visiblePaths.length === 0) return null;

  return (
    <Menu
      mode="horizontal"
      selectedKeys={selectedKeys}
      items={items}
      style={{ marginBottom: 0, borderBottom: '1px solid rgba(5, 5, 5, 0.06)' }}
    />
  );
}
