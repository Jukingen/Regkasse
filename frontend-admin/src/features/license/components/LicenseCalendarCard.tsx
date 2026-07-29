'use client';

import { MailOutlined } from '@ant-design/icons';
import { useMutation } from '@tanstack/react-query';
import {
  Button,
  Card,
  Col,
  Empty,
  Flex,
  Row,
  Skeleton,
  Statistic,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import React, { useMemo, useState } from 'react';

import { dateColumnRender } from '@/components/DateColumn';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useLicenseDashboardStats } from '@/features/license/api/licenseStats';
import type { TenantLicenseOverviewItem } from '@/features/license/api/tenantLicenseOverview';
import { useTenantLicenseOverview } from '@/features/license/hooks/useTenantLicenseOverview';
import {
  type MandantLicenseOverviewKind,
  mandantLicenseOverviewKindLabelKey,
  mandantLicenseOverviewTagColor,
} from '@/features/license/utils/mandantLicenseOverviewStatus';
import { sendAdminTenantLicenseReminder } from '@/features/super-admin/api/adminTenantLicense';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

const KPI_TILE = {
  active: { background: '#f6ffed', color: '#389e0d' },
  expiring: { background: '#fffbe6', color: '#d48806' },
  expired: { background: '#fff2f0', color: '#cf1322' },
} as const;

export function LicenseCalendarCard() {
  const { t } = useI18n();
  const notify = useNotify();
  const { user } = useAuth();
  const isSuperAdminUser = isSuperAdmin(user?.role);
  const [showAll, setShowAll] = useState(true);
  const [pendingTenantId, setPendingTenantId] = useState<string | null>(null);

  const statsQuery = useLicenseDashboardStats({ enabled: isSuperAdminUser });
  const overviewQuery = useTenantLicenseOverview(isSuperAdminUser);

  const reminderMutation = useMutation({
    mutationFn: (row: TenantLicenseOverviewItem) =>
      sendAdminTenantLicenseReminder(row.tenantId),
    onMutate: (row) => setPendingTenantId(row.tenantId),
    onSettled: () => setPendingTenantId(null),
    onSuccess: (result, row) => {
      notify.successKey('license.calendar.reminderSuccess', {
        recipient: result.recipientEmail || row.tenantName,
      });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'LicenseCalendarCard.sendReminder',
        fallbackKey: 'license.calendar.reminderError',
      });
    },
  });

  const tableRows = useMemo(() => {
    const rows = overviewQuery.data ?? [];
    if (showAll) return rows;
    return rows.filter(
      (row) => row.status === 'expiring_soon' || row.status === 'expired'
    );
  }, [overviewQuery.data, showAll]);

  const columns = useMemo<ColumnsType<TenantLicenseOverviewItem>>(
    () => [
      {
        title: t('license.calendar.columns.tenant'),
        dataIndex: 'tenantName',
        key: 'tenantName',
        sorter: (a, b) => a.tenantName.localeCompare(b.tenantName, 'de'),
        render: (name: string, row) => (
          <Flex vertical gap={0}>
            <Typography.Text strong>{name}</Typography.Text>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {row.tenantSlug}
            </Typography.Text>
          </Flex>
        ),
      },
      {
        title: t('license.calendar.columns.validUntil'),
        dataIndex: 'validUntilUtc',
        key: 'validUntilUtc',
        width: 140,
        sorter: (a, b) => {
          const left = a.validUntilUtc ? dayjs(a.validUntilUtc).unix() : 0;
          const right = b.validUntilUtc ? dayjs(b.validUntilUtc).unix() : 0;
          return left - right;
        },
        render: dateColumnRender('short'),
      },
      {
        title: t('license.calendar.columns.status'),
        dataIndex: 'status',
        key: 'status',
        width: 160,
        render: (kind: MandantLicenseOverviewKind) => (
          <Tag color={mandantLicenseOverviewTagColor(kind)}>
            {t(mandantLicenseOverviewKindLabelKey(kind))}
          </Tag>
        ),
      },
      {
        title: t('license.calendar.columns.action'),
        key: 'action',
        width: 160,
        render: (_, row) => (
          <Button
            type="link"
            size="small"
            icon={<MailOutlined />}
            disabled={!row.hasOwnerAdmin}
            loading={reminderMutation.isPending && pendingTenantId === row.tenantId}
            onClick={() => reminderMutation.mutate(row)}
          >
            {t('license.calendar.sendReminder')}
          </Button>
        ),
      },
    ],
    [pendingTenantId, reminderMutation, t]
  );

  if (!isSuperAdminUser) {
    return null;
  }

  const statsLoading = statsQuery.isLoading;
  const stats = statsQuery.data;

  return (
    <Card
      title={t('license.calendar.title')}
      style={{ marginTop: 24 }}
      extra={
        <Button type="link" size="small" onClick={() => setShowAll((v) => !v)}>
          {showAll
            ? t('license.calendar.filterAttentionOnly')
            : t('license.calendar.filterShowAll')}
        </Button>
      }
    >
      <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
        {t('license.calendar.subtitle')}
      </Typography.Paragraph>

      {statsQuery.isError ? (
        <Typography.Paragraph type="danger">
          {t('license.calendar.loadFailed')}
        </Typography.Paragraph>
      ) : null}

      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={24} sm={8}>
          {statsLoading ? (
            <Skeleton active paragraph={{ rows: 1 }} title={{ width: '60%' }} />
          ) : (
            <Card size="small" styles={{ body: { background: KPI_TILE.active.background } }}>
              <Statistic
                title={t('license.calendar.kpiActive')}
                value={stats?.activeTenantLicenses ?? 0}
                valueStyle={{ color: KPI_TILE.active.color }}
              />
            </Card>
          )}
        </Col>
        <Col xs={24} sm={8}>
          {statsLoading ? (
            <Skeleton active paragraph={{ rows: 1 }} title={{ width: '60%' }} />
          ) : (
            <Card size="small" styles={{ body: { background: KPI_TILE.expiring.background } }}>
              <Statistic
                title={t('license.calendar.kpiExpiring')}
                value={stats?.expiringTenantLicenses ?? 0}
                valueStyle={{ color: KPI_TILE.expiring.color }}
              />
            </Card>
          )}
        </Col>
        <Col xs={24} sm={8}>
          {statsLoading ? (
            <Skeleton active paragraph={{ rows: 1 }} title={{ width: '60%' }} />
          ) : (
            <Card size="small" styles={{ body: { background: KPI_TILE.expired.background } }}>
              <Statistic
                title={t('license.calendar.kpiExpired')}
                value={stats?.expiredTenantLicenses ?? 0}
                valueStyle={{ color: KPI_TILE.expired.color }}
              />
            </Card>
          )}
        </Col>
      </Row>

      <Table<TenantLicenseOverviewItem>
        rowKey="tenantId"
        size="small"
        loading={overviewQuery.isLoading}
        dataSource={tableRows}
        columns={columns}
        pagination={{ pageSize: 10, showSizeChanger: false }}
        locale={{
          emptyText: (
            <Empty
              image={Empty.PRESENTED_IMAGE_SIMPLE}
              description={t('license.calendar.empty')}
            />
          ),
        }}
      />
    </Card>
  );
}
