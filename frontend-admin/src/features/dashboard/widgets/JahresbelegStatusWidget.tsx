'use client';

import { Alert, Table, Tag, Typography } from 'antd';
import Link from 'next/link';
import React, { useMemo } from 'react';

import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import {
  useJahresbelegTenantStatus,
  type JahresbelegTenantStatus,
} from '@/features/dashboard/hooks/useJahresbelegTenantStatus';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useI18n } from '@/i18n/I18nProvider';
import { PERMISSIONS } from '@/shared/auth/permissions';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps'>;

export function JahresbelegStatusWidget({ title, dragHandleProps }: Props) {
  const { t } = useI18n();
  const { isAuthorized } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.RKSV_JAHRESBELEG_VIEW,
  });
  const query = useJahresbelegTenantStatus(isAuthorized);
  const rows = query.data ?? [];
  const missing = rows.some((row) => row.status === 'missing');

  const columns = useMemo(
    () => [
      { title: t('dashboard.jahresbelegStatus.tenant'), dataIndex: 'tenantName', key: 'tenantName' },
      { title: t('dashboard.jahresbelegStatus.year'), dataIndex: 'year', key: 'year', width: 90 },
      {
        title: t('dashboard.jahresbelegStatus.deadline'),
        dataIndex: 'deadline',
        key: 'deadline',
        width: 130,
      },
      {
        title: t('dashboard.jahresbelegStatus.status'),
        dataIndex: 'status',
        key: 'status',
        width: 140,
        render: (status: JahresbelegTenantStatus['status']) => (
          <Tag color={status === 'missing' ? 'gold' : status === 'created' ? 'green' : 'default'}>
            {t(`dashboard.jahresbelegStatus.${status}`)}
          </Tag>
        ),
      },
    ],
    [t]
  );

  return (
    <WidgetShell
      title={title}
      dragHandleProps={dragHandleProps}
      onRefresh={() => void query.refetch()}
      refreshing={query.isFetching}
    >
      {query.isError ? (
        <Alert type="error" showIcon title={t('dashboard.jahresbelegStatus.loadError')} />
      ) : (
        <>
          {missing ? (
            <Alert
              type="warning"
              showIcon
              style={{ marginBottom: 12 }}
              title={t('dashboard.jahresbelegStatus.warning')}
            />
          ) : null}
          <Table<JahresbelegTenantStatus>
            size="small"
            rowKey="tenantId"
            loading={query.isLoading}
            columns={columns}
            dataSource={rows}
            pagination={false}
            locale={{ emptyText: t('dashboard.jahresbelegStatus.empty') }}
            data-testid="jahresbeleg-status-table"
          />
          <Typography.Paragraph style={{ marginTop: 12, marginBottom: 0 }}>
            <Link href="/rksv/jahresbeleg">{t('dashboard.jahresbelegStatus.open')}</Link>
          </Typography.Paragraph>
        </>
      )}
    </WidgetShell>
  );
}
