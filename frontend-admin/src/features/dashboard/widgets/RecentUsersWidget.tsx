'use client';

import { Typography } from 'antd';
import React, { useMemo } from 'react';

import { SimpleList as List } from '@/components/ui/SimpleList';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { listAllAdminUsers } from '@/features/users/api/users';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { useI18n } from '@/i18n/I18nProvider';
import { formatDateTime } from '@/i18n/formatting';
import { PERMISSIONS } from '@/shared/auth/permissions';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'>;

export function RecentUsersWidget({ title, dragHandleProps, onRefresh }: Props) {
  const { t, formatLocale } = useI18n();
  const query = useAuthorizedQuery({
    queryKey: ['dashboard', 'recent-users'],
    queryFn: () => listAllAdminUsers({ isActive: true }),
    requiredPermission: PERMISSIONS.USER_VIEW,
    refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
    staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
  });

  const recent = useMemo(
    () =>
      [...(query.data ?? [])]
        .filter((u) => u.createdAt)
        .sort((a, b) => new Date(b.createdAt!).getTime() - new Date(a.createdAt!).getTime())
        .slice(0, 5),
    [query.data]
  );

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
        dataSource={recent}
        locale={{ emptyText: t('dashboard.widgets.recentUsers.empty') }}
        renderItem={(user) => {
          const name =
            [user.firstName, user.lastName].filter(Boolean).join(' ') ||
            user.userName ||
            user.email ||
            user.id;
          return (
            <List.Item>
              <List.Item.Meta
                title={name}
                description={
                  <Typography.Text type="secondary">
                    {user.role ?? '—'}
                    {user.createdAt ? ` · ${formatDateTime(user.createdAt, formatLocale)}` : ''}
                  </Typography.Text>
                }
              />
            </List.Item>
          );
        }}
      />
    </WidgetShell>
  );
}
