'use client';

import { Button, Typography } from 'antd';
import Link from 'next/link';
import React, { useMemo } from 'react';

import { SimpleList as List } from '@/components/ui/SimpleList';
import { getPermissionAuditLogs } from '@/features/audit/api/permissionAudit';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { useI18n } from '@/i18n/I18nProvider';
import { formatDateTime } from '@/i18n/formatting';
import { PERMISSIONS } from '@/shared/auth/permissions';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'>;

function actionEmoji(action: string): string {
  switch (action) {
    case 'created':
      return '➕';
    case 'deleted':
      return '🗑️';
    case 'reverted':
      return '⟲';
    default:
      return '🔄';
  }
}

export function RecentPermissionChangesWidget({ title, dragHandleProps, onRefresh }: Props) {
  const { t, formatLocale } = useI18n();
  const query = useAuthorizedQuery({
    queryKey: ['dashboard', 'recent-permission-changes'],
    queryFn: () => getPermissionAuditLogs({ page: 1, pageSize: 5 }),
    requiredPermission: PERMISSIONS.AUDIT_VIEW,
    refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
    staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
  });

  const items = useMemo(() => query.data?.items ?? [], [query.data?.items]);

  const handleRefresh = () => {
    void query.refetch();
    onRefresh?.();
  };

  return (
    <WidgetShell
      title={title}
      dragHandleProps={dragHandleProps}
      onRefresh={handleRefresh}
      refreshing={query.isFetching}
    >
      <List
        size="small"
        loading={query.isLoading}
        dataSource={items}
        locale={{ emptyText: t('dashboard.widgets.recentPermissionChanges.empty') }}
        renderItem={(entry) => {
          const when = entry.timestamp
            ? formatDateTime(entry.timestamp, formatLocale)
            : '—';
          const clock = (() => {
            try {
              return new Intl.DateTimeFormat(formatLocale, {
                hour: '2-digit',
                minute: '2-digit',
              }).format(new Date(entry.timestamp));
            } catch {
              return when;
            }
          })();
          const label =
            entry.action === 'updated' && entry.permissionKey
              ? t('dashboard.widgets.recentPermissionChanges.permissionLine', {
                  permission: entry.permissionKey,
                })
              : t('dashboard.widgets.recentPermissionChanges.roleLine', {
                  role: entry.roleName || '—',
                });

          return (
            <List.Item>
              <List.Item.Meta
                title={
                  <Typography.Text>
                    <span style={{ marginRight: 8, color: 'rgba(0,0,0,0.45)', fontVariantNumeric: 'tabular-nums' }}>
                      {clock}
                    </span>
                    {actionEmoji(entry.action)} {label}
                  </Typography.Text>
                }
                description={
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {entry.actorName || t('users.permissionAudit.systemActor')}
                    {entry.roleName ? ` · ${entry.roleName}` : ''}
                  </Typography.Text>
                }
              />
            </List.Item>
          );
        }}
      />
      <div style={{ marginTop: 8, textAlign: 'right' }}>
        <Link href="/admin/access/permission-history" prefetch={false}>
          <Button type="link" size="small" style={{ paddingInline: 0 }}>
            {t('dashboard.widgets.recentPermissionChanges.viewAll')}
          </Button>
        </Link>
      </div>
    </WidgetShell>
  );
}
