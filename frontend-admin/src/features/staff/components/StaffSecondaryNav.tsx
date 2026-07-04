'use client';

import React, { useMemo, type ComponentType } from 'react';
import { Menu } from 'antd';
import type { MenuProps } from 'antd';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
    TeamOutlined,
    BarChartOutlined,
    ClockCircleOutlined,
    AppstoreOutlined,
} from '@ant-design/icons';
import { useI18n } from '@/i18n/I18nProvider';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import {
    STAFF_AREA_ROUTE_PATHS,
    type StaffAreaRoutePath,
} from '@/shared/staffAreaRoutes';

const STAFF_TAB_META: Record<
    StaffAreaRoutePath,
    { labelKey: string; Icon: ComponentType }
> = {
    '/staff': { labelKey: 'staff:nav.overview', Icon: AppstoreOutlined },
    '/staff/list': { labelKey: 'staff:nav.list', Icon: TeamOutlined },
    '/staff/performance': { labelKey: 'staff:nav.performance', Icon: BarChartOutlined },
    '/staff/shifts': { labelKey: 'staff:nav.shifts', Icon: ClockCircleOutlined },
};

export function StaffSecondaryNav() {
    const pathname = usePathname() ?? '';
    const { t } = useI18n();
    const { user } = useAuth();
    const permissions = user?.permissions ?? [];

    const visiblePaths = useMemo(
        () => STAFF_AREA_ROUTE_PATHS.filter((path) => isMenuItemAllowed(path, permissions)),
        [permissions],
    );

    const items: MenuProps['items'] = useMemo(
        () =>
            visiblePaths.map((path) => {
                const { labelKey, Icon } = STAFF_TAB_META[path];
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
