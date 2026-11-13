'use client';

import { Button, Card, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';

import type { CriticalUserDto, CriticalUserStatus } from '@/features/tenants/api/tenantLimits';
import {
  healthStatusI18nKey,
  healthTagColor,
  limitDashboardDetailHref,
  limitDashboardLabelKey,
} from '@/features/tenants/components/limit-dashboard/limitDashboardShared';
import { formatRoleDisplayLabel } from '@/features/users/utils/roleDisplayLabel';
import { useI18n } from '@/i18n';

export function CriticalUsersTable({
  users,
  showTenant,
  isSuperAdmin,
  onOpenDetail,
}: {
  users: CriticalUserDto[];
  showTenant: boolean;
  isSuperAdmin: boolean;
  onOpenDetail: (href: string) => void;
}) {
  const { t } = useI18n();

  const limitName = (key: string) => {
    const i18nKey = limitDashboardLabelKey(key);
    const label = t(i18nKey);
    return label === i18nKey ? key : label;
  };

  const columns: ColumnsType<CriticalUserDto> = [
    ...(showTenant
      ? [
          {
            title: t('tenants.limits.dashboard.tenant'),
            dataIndex: 'tenantName',
            key: 'tenantName',
            render: (name: string | null | undefined, row: CriticalUserDto) => name || row.tenantId,
          },
        ]
      : []),
    {
      title: t('tenants.limits.dashboard.users.user'),
      key: 'user',
      render: (_: unknown, row: CriticalUserDto) => (
        <Space orientation="vertical" size={0}>
          <span>{row.displayName || row.userName}</span>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {formatRoleDisplayLabel(t, row.role)}
          </Typography.Text>
        </Space>
      ),
    },
    {
      title: t('tenants.limits.dashboard.users.limit'),
      dataIndex: 'limitKey',
      key: 'limitKey',
      render: (key: string) => limitName(key),
    },
    {
      title: t('tenants.limits.dashboard.users.usage'),
      key: 'usage',
      render: (_: unknown, row: CriticalUserDto) =>
        `${row.current} / ${row.limit} (${row.percentage}%)`,
    },
    {
      title: t('tenants.limits.dashboard.users.status'),
      dataIndex: 'status',
      key: 'status',
      render: (status: CriticalUserStatus) => (
        <Tag color={healthTagColor(status)}>{t(healthStatusI18nKey(status))}</Tag>
      ),
    },
    {
      title: t('tenants.limits.dashboard.users.action'),
      key: 'action',
      render: (_: unknown, row: CriticalUserDto) => (
        <Space orientation="vertical" size={4}>
          <Typography.Text>{row.recommendedAction}</Typography.Text>
          <Button
            size="small"
            type="link"
            onClick={() =>
              onOpenDetail(limitDashboardDetailHref(row.limitKey, row.tenantId, isSuperAdmin))
            }
          >
            {t('tenants.limits.dashboard.limits.openDetail')}
          </Button>
        </Space>
      ),
    },
  ];

  return (
    <Card variant="borderless" title={t('tenants.limits.dashboard.users.title')}>
      <Table
        rowKey={(row) => `${row.tenantId}-${row.userId}-${row.limitKey}`}
        columns={columns}
        dataSource={users}
        pagination={false}
        locale={{ emptyText: t('tenants.limits.dashboard.emptyUsers') }}
      />
    </Card>
  );
}
