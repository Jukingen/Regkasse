'use client';

import { MailOutlined, ReloadOutlined } from '@ant-design/icons';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Col, Row, Space, Statistic, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import React, { useMemo } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  type GracePeriodTenantRow,
  gracePeriodDashboardQueryKey,
  useGracePeriodDashboard,
} from '@/features/license/api/gracePeriodDashboard';
import { sendAdminTenantLicenseReminder } from '@/features/super-admin/api/adminTenantLicense';
import { useNotify } from '@/hooks/useNotify';
import { formatGermanDate, useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

function daysTagColor(days: number): string {
  if (days <= 2) return 'red';
  if (days <= 5) return 'orange';
  return 'green';
}

/**
 * Super Admin grace-period cohort dashboard (KPI buckets + tenant table + reminder CTA).
 */
export function GracePeriodDashboardPageContent() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const dashboardQuery = useGracePeriodDashboard();

  const reminderMutation = useMutation({
    mutationFn: (tenantId: string) => sendAdminTenantLicenseReminder(tenantId),
    onSuccess: (result) => {
      notify.successKey('license.gracePeriodDashboard.reminderSent', {
        email: result.recipientEmail,
      });
      void queryClient.invalidateQueries({ queryKey: gracePeriodDashboardQueryKey });
    },
    onError: (error) => {
      notify.apiError(error, {
        logContext: 'GracePeriodDashboard.sendReminder',
        fallbackKey: 'license.gracePeriodDashboard.reminderFailed',
      });
    },
  });

  const columns: ColumnsType<GracePeriodTenantRow> = useMemo(
    () => [
      {
        title: t('license.gracePeriodDashboard.columns.tenant'),
        dataIndex: 'name',
        key: 'name',
        render: (_value, row) => (
          <Space orientation="vertical" size={0}>
            <Link href={`/admin/tenants/${row.id}`}>{row.name}</Link>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {row.slug}
            </Typography.Text>
          </Space>
        ),
      },
      {
        title: t('license.gracePeriodDashboard.columns.expiredAt'),
        dataIndex: 'expiredAtUtc',
        key: 'expiredAtUtc',
        render: (value: string) => formatGermanDate(value),
      },
      {
        title: t('license.gracePeriodDashboard.columns.lockdownAt'),
        dataIndex: 'lockdownAtUtc',
        key: 'lockdownAtUtc',
        render: (value: string) => formatGermanDate(value),
      },
      {
        title: t('license.gracePeriodDashboard.columns.daysRemaining'),
        dataIndex: 'daysRemaining',
        key: 'daysRemaining',
        sorter: (a, b) => a.daysRemaining - b.daysRemaining,
        defaultSortOrder: 'ascend',
        render: (days: number) => (
          <Tag color={daysTagColor(days)}>
            {t('license.gracePeriodDashboard.daysTag', { days })}
          </Tag>
        ),
      },
      {
        title: t('license.gracePeriodDashboard.columns.actions'),
        key: 'actions',
        render: (_value, row) => (
          <Button
            size="small"
            icon={<MailOutlined />}
            loading={reminderMutation.isPending && reminderMutation.variables === row.id}
            onClick={() => reminderMutation.mutate(row.id)}
          >
            {t('license.gracePeriodDashboard.sendReminder')}
          </Button>
        ),
      },
    ],
    [reminderMutation, t]
  );

  const data = dashboardQuery.data;

  return (
    <div>
      <AdminPageHeader
        title={t('license.gracePeriodDashboard.title')}
        subtitle={t('license.gracePeriodDashboard.subtitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          {
            title: t('nav.licenseManagement'),
            href: '/admin/license-management',
          },
          { title: t('license.gracePeriodDashboard.title') },
        ]}
        extra={
          <Button
            icon={<ReloadOutlined />}
            loading={dashboardQuery.isFetching}
            onClick={() => void dashboardQuery.refetch()}
          >
            {t('common.buttons.refresh')}
          </Button>
        }
      />

      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('license.gracePeriodDashboard.stats.total')}
              value={data?.total ?? 0}
              loading={dashboardQuery.isLoading}
              styles={{ content: { color: '#faad14' } }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('license.gracePeriodDashboard.stats.critical')}
              value={data?.critical ?? 0}
              loading={dashboardQuery.isLoading}
              styles={{ content: { color: '#cf1322' } }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('license.gracePeriodDashboard.stats.medium')}
              value={data?.medium ?? 0}
              loading={dashboardQuery.isLoading}
              styles={{ content: { color: '#faad14' } }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('license.gracePeriodDashboard.stats.good')}
              value={data?.good ?? 0}
              loading={dashboardQuery.isLoading}
              styles={{ content: { color: '#52c41a' } }}
            />
          </Card>
        </Col>
      </Row>

      <Card title={t('license.gracePeriodDashboard.tableTitle')}>
        <Table<GracePeriodTenantRow>
          rowKey="id"
          loading={dashboardQuery.isLoading}
          dataSource={data?.list ?? []}
          columns={columns}
          pagination={{ pageSize: 20, showSizeChanger: true }}
        />
      </Card>
    </div>
  );
}
