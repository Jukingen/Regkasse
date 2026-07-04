'use client';

import { useMemo } from 'react';
import { usePathname, useRouter } from 'next/navigation';
import { Tabs } from 'antd';

import { useI18n } from '@/i18n';

type TabKey = 'compliance' | 'activity' | 'staff';

const TAB_HREF: Record<TabKey, string> = {
    compliance: '/audit-logs',
    activity: '/audit-logs/activity',
    staff: '/audit-logs/staff',
};

function resolveActiveTab(pathname: string | null): TabKey {
    if (!pathname) return 'compliance';
    if (pathname.startsWith('/audit-logs/activity')) return 'activity';
    if (pathname.startsWith('/audit-logs/staff')) return 'staff';
    return 'compliance';
}

export function AuditLogsSubNav() {
    const pathname = usePathname();
    const router = useRouter();
    const { t } = useI18n();

    const activeKey = resolveActiveTab(pathname);

    const items = useMemo(
        () => [
            { key: 'compliance' as const, label: t('activity.tabs.compliance') },
            { key: 'activity' as const, label: t('activity.tabs.activity') },
            { key: 'staff' as const, label: t('activity.tabs.staff') },
        ],
        [t],
    );

    return (
        <Tabs
            activeKey={activeKey}
            onChange={(key) => router.push(TAB_HREF[key as TabKey])}
            items={items}
            style={{ marginBottom: 16 }}
        />
    );
}
