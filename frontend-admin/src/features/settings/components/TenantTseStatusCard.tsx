'use client';

import { useQuery } from '@tanstack/react-query';
import {
  Button,
  Card,
  Modal,
  Space,
  Statistic,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { useState } from 'react';

import {
  getTenantTseHealthHistory,
  getTenantTseStatus,
  type TenantTseDeviceStatus,
  type TenantTseHealthHistoryPoint,
} from '@/features/settings/api/tenantTseClient';
import { useI18n } from '@/i18n';

const STATUS_KEY = ['tenant', 'tse', 'status'] as const;
const HISTORY_KEY = ['tenant', 'tse', 'health-history'] as const;

export function TenantTseStatusCard() {
  const { t } = useI18n();
  const [detailsOpen, setDetailsOpen] = useState(false);

  const statusQuery = useQuery({
    queryKey: STATUS_KEY,
    queryFn: ({ signal }) => getTenantTseStatus(signal),
    staleTime: 30_000,
  });

  const historyQuery = useQuery({
    queryKey: [...HISTORY_KEY, 30],
    queryFn: ({ signal }) => getTenantTseHealthHistory(30, signal),
    enabled: detailsOpen,
  });

  const status = statusQuery.data;
  const healthStatus = status?.overallHealth ?? 'Unknown';
  const healthScore = status?.overallHealthScore ?? 0;
  const daysUntilExpiry = status?.nearestDaysUntilExpiry;

  const deviceColumns: ColumnsType<TenantTseDeviceStatus> = [
    {
      title: t('settings.manager.tsePortal.colSerial'),
      dataIndex: 'serialNumber',
      key: 'serial',
      render: (v: string | null | undefined) => v || '—',
    },
    {
      title: t('settings.manager.tsePortal.colRole'),
      key: 'role',
      render: (_, row) =>
        row.isPrimary
          ? t('settings.manager.tsePortal.primary')
          : row.isBackup
            ? t('settings.manager.tsePortal.backup')
            : '—',
    },
    {
      title: t('settings.manager.tsePortal.colStatus'),
      dataIndex: 'healthStatus',
      key: 'healthStatus',
      render: (s: string) => (
        <Tag color={s === 'Healthy' ? 'green' : s === 'Degraded' ? 'orange' : 'red'}>{s}</Tag>
      ),
    },
    {
      title: t('settings.manager.tsePortal.colScore'),
      dataIndex: 'healthScore',
      key: 'healthScore',
    },
    {
      title: t('settings.manager.tsePortal.colExpiry'),
      dataIndex: 'daysUntilExpiry',
      key: 'expiry',
      render: (d: number | null | undefined, row) =>
        d == null
          ? row.expiresAt
            ? dayjs(row.expiresAt).format('DD.MM.YYYY')
            : '—'
          : t('settings.manager.tsePortal.daysValue', { days: d }),
    },
  ];

  const historyColumns: ColumnsType<TenantTseHealthHistoryPoint> = [
    {
      title: t('settings.manager.tsePortal.colTime'),
      dataIndex: 'checkedAtUtc',
      key: 'time',
      render: (v: string) => dayjs(v).format('DD.MM.YYYY HH:mm'),
    },
    {
      title: t('settings.manager.tsePortal.colSerial'),
      dataIndex: 'serialNumber',
      key: 'serial',
      render: (v: string | null | undefined) => v || '—',
    },
    {
      title: t('settings.manager.tsePortal.colScore'),
      dataIndex: 'healthScore',
      key: 'score',
    },
    {
      title: t('settings.manager.tsePortal.colStatus'),
      dataIndex: 'healthStatus',
      key: 'status',
    },
  ];

  return (
    <>
      <Card title={t('settings.manager.tsePortal.title')} loading={statusQuery.isLoading}>
        {statusQuery.isError ? (
          <Typography.Text type="danger">{t('settings.manager.tsePortal.loadError')}</Typography.Text>
        ) : (
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 24,
              flexWrap: 'wrap',
              padding: 16,
              background: 'var(--ant-color-fill-quaternary, #fafafa)',
              borderRadius: 8,
            }}
          >
            <Tag color={healthStatus === 'Healthy' ? 'green' : healthStatus === 'Unknown' ? 'default' : 'red'}>
              {healthStatus === 'Healthy'
                ? t('settings.manager.tsePortal.healthy')
                : healthStatus === 'Degraded'
                  ? t('settings.manager.tsePortal.degraded')
                  : t('settings.manager.tsePortal.unknown')}
            </Tag>
            <Statistic
              title={t('settings.manager.tsePortal.healthScore')}
              value={healthScore}
              suffix="%"
              valueStyle={{ color: healthScore > 80 ? '#52c41a' : '#faad14' }}
            />
            <Statistic
              title={t('settings.manager.tsePortal.daysUntilExpiry')}
              value={daysUntilExpiry ?? '—'}
              valueStyle={{
                color:
                  daysUntilExpiry == null
                    ? undefined
                    : daysUntilExpiry > 30
                      ? '#52c41a'
                      : '#faad14',
              }}
            />
            <Button type="link" onClick={() => setDetailsOpen(true)}>
              {t('settings.manager.tsePortal.viewDetails')}
            </Button>
          </div>
        )}
      </Card>

      <Modal
        open={detailsOpen}
        title={t('settings.manager.tsePortal.detailsTitle')}
        onCancel={() => setDetailsOpen(false)}
        footer={
          <Button onClick={() => setDetailsOpen(false)}>
            {t('settings.manager.tsePortal.close')}
          </Button>
        }
        width={860}
        destroyOnHidden
      >
        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
          <Table
            rowKey="deviceId"
            size="small"
            pagination={false}
            dataSource={status?.devices ?? []}
            columns={deviceColumns}
          />
          <Typography.Title level={5} style={{ marginBottom: 0 }}>
            {t('settings.manager.tsePortal.historyTitle')}
          </Typography.Title>
          <Table
            rowKey={(row) => `${row.deviceId}-${row.checkedAtUtc}-${row.healthScore}`}
            size="small"
            loading={historyQuery.isLoading}
            pagination={{ pageSize: 10 }}
            dataSource={historyQuery.data?.points ?? []}
            columns={historyColumns}
          />
        </Space>
      </Modal>
    </>
  );
}
