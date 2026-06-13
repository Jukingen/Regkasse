'use client';

import React, { useMemo, type ComponentType } from 'react';
import { Menu } from 'antd';
import type { MenuProps } from 'antd';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
    KeyOutlined,
    TeamOutlined,
    SafetyOutlined,
    AuditOutlined,
} from '@ant-design/icons';
import { useI18n } from '@/i18n/I18nProvider';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import {
    ACCESS_AREA_ROUTE_PATHS,
    type AccessAreaRoutePath,
} from '@/shared/accessAreaRoutes';

const ACCESS_TAB_META: Record<
    AccessAreaRoutePath,
    { labelKey: string; Icon: ComponentType }
> = {
    '/admin/access': { labelKey: 'nav.accessOverview', Icon: KeyOutlined },
    '/admin/users': { labelKey: 'nav.users', Icon: TeamOutlined },
    '/admin/access/roles': { labelKey: 'nav.accessRoles', Icon: SafetyOutlined },
    '/admin/access/matrix': { labelKey: 'nav.accessMatrix', Icon: AuditOutlined },
};

export function AccessSecondaryNav() {
    const pathname = usePathname() ?? '';
    const { t } = useI18n();
    const { user } = useAuth();
    const permissions = user?.permissions ?? [];

    const visiblePaths = useMemo(
        () => ACCESS_AREA_ROUTE_PATHS.filter((path) => isMenuItemAllowed(path, permissions)),
        [permissions],
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
