'use client';

import React, { useMemo, type ComponentType } from 'react';
import { Menu } from 'antd';
import type { MenuProps } from 'antd';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
    DashboardOutlined,
    DatabaseOutlined,
    SettingOutlined,
    AuditOutlined,
    LineChartOutlined,
    HistoryOutlined,
    SafetyCertificateOutlined,
    EuroCircleOutlined,
} from '@ant-design/icons';
import { useI18n } from '@/i18n/I18nProvider';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useBackupPermissions } from '@/features/backup/hooks/useBackupPermissions';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import {
    BACKUP_AUDIT_PATH,
    BACKUP_COMPLIANCE_PATH,
    BACKUP_CONFIGURATION_PATH,
    BACKUP_COSTS_PATH,
    BACKUP_DASHBOARD_PATH,
    BACKUP_HUB_LANDING_PATH,
    BACKUP_PERFORMANCE_PATH,
    BACKUP_RESTORE_HISTORY_PATH,
    BACKUP_RUNS_PATH,
    BACKUP_SECONDARY_NAV_ITEMS,
    backupPathFromPathname,
    isBackupAreaPath,
} from '@/shared/backupAreaRoutes';

const BACKUP_TAB_META: Record<
    (typeof BACKUP_SECONDARY_NAV_ITEMS)[number]['id'],
    { Icon: ComponentType }
> = {
    overview: { Icon: DashboardOutlined },
    performance: { Icon: LineChartOutlined },
    compliance: { Icon: SafetyCertificateOutlined },
    costs: { Icon: EuroCircleOutlined },
    restoreHistory: { Icon: HistoryOutlined },
    runs: { Icon: DatabaseOutlined },
    configuration: { Icon: SettingOutlined },
    auditLog: { Icon: AuditOutlined },
};

const NAV_ID_BY_PATH: Record<string, (typeof BACKUP_SECONDARY_NAV_ITEMS)[number]['id']> = {
    [BACKUP_HUB_LANDING_PATH]: 'overview',
    [BACKUP_DASHBOARD_PATH]: 'overview',
    [BACKUP_PERFORMANCE_PATH]: 'performance',
    [BACKUP_COMPLIANCE_PATH]: 'compliance',
    [BACKUP_COSTS_PATH]: 'costs',
    [BACKUP_RESTORE_HISTORY_PATH]: 'restoreHistory',
    [BACKUP_RUNS_PATH]: 'runs',
    [BACKUP_CONFIGURATION_PATH]: 'configuration',
    [BACKUP_AUDIT_PATH]: 'auditLog',
};

export function BackupSecondaryNav() {
    const pathname = usePathname() ?? '';
    const { t } = useI18n();
    const { user } = useAuth();
    const { canRestore } = useBackupPermissions();
    const permissions = user?.permissions ?? [];
    const inBackupArea = isBackupAreaPath(pathname);

    const visibleItems = useMemo(
        () =>
            BACKUP_SECONDARY_NAV_ITEMS.filter((item) => {
                if (item.id === 'restoreHistory' && !canRestore) return false;
                return isMenuItemAllowed(item.menuKey, permissions);
            }),
        [canRestore, permissions],
    );

    const items: MenuProps['items'] = useMemo(
        () =>
            visibleItems.map((item) => {
                const { Icon } = BACKUP_TAB_META[item.id];
                return {
                    key: item.id,
                    icon: <Icon />,
                    label: (
                        <Link href={item.href} prefetch={false}>
                            {t(item.labelKey)}
                        </Link>
                    ),
                };
            }),
        [t, visibleItems],
    );

    const selectedKeys = useMemo(() => {
        const canonical = backupPathFromPathname(pathname);
        if (!canonical) return ['overview'];
        const id = NAV_ID_BY_PATH[canonical];
        return id ? [id] : ['overview'];
    }, [pathname]);

    if (!inBackupArea || visibleItems.length === 0) {
        return null;
    }

    return (
        <Menu
            mode="horizontal"
            selectedKeys={selectedKeys}
            items={items}
            style={{ marginBottom: 0, borderBottom: '1px solid rgba(5, 5, 5, 0.06)' }}
        />
    );
}
