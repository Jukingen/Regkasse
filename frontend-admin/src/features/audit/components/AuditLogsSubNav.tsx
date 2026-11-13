'use client';

import { Tabs } from 'antd';
import { usePathname, useRouter } from 'next/navigation';
import { useMemo } from 'react';

import { useI18n } from '@/i18n';

type TabKey = 'compliance' | 'activity' | 'staff' | 'operations';

const TAB_HREF: Record<TabKey, string> = {
  compliance: '/audit-logs',
  activity: '/audit-logs/activity',
  staff: '/audit-logs/staff',
  operations: '/audit-logs/operations',
};

function resolveActiveTab(pathname: string | null): TabKey {
  if (!pathname) return 'compliance';
  if (pathname.startsWith('/audit-logs/activity')) return 'activity';
  if (pathname.startsWith('/audit-logs/staff')) return 'staff';
  if (pathname.startsWith('/audit-logs/operations')) return 'operations';
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
      { key: 'operations' as const, label: t('activity.tabs.operations') },
      { key: 'staff' as const, label: t('activity.tabs.staff') },
    ],
    [t]
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
