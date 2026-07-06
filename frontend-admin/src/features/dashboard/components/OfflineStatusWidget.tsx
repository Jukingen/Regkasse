'use client';

import React, { useMemo } from 'react';
import { Badge, Button, Progress, Space, Statistic } from 'antd';
import {
  CheckCircleOutlined,
  CloudSyncOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import Link from 'next/link';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import 'dayjs/locale/de';
import 'dayjs/locale/en';
import 'dayjs/locale/tr';

import { OFFLINE_PENDING_ORDERS_CAP } from '@/features/offline/api/offlineMonitoringApi';
import { useOfflineMonitoring } from '@/features/offline/hooks/useOfflineMonitoring';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { useI18n } from '@/i18n/I18nProvider';

dayjs.extend(relativeTime);

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'>;

function pendingOrdersColor(count: number): string {
  if (count > 50) return '#dc2626';
  if (count > 20) return '#eab308';
  return '#16a34a';
}

function successRateColor(rate: number): string {
  if (rate > 90) return '#16a34a';
  if (rate > 70) return '#eab308';
  return '#dc2626';
}

function dayjsLocale(code: string): string {
  if (code.startsWith('tr')) return 'tr';
  if (code.startsWith('en')) return 'en';
  return 'de';
}

export function OfflineStatusWidget({ title, dragHandleProps, onRefresh }: Props) {
  const { t, textLocale } = useI18n();
  const query = useOfflineMonitoring();
  const data = query.data;

  const relativeLocale = dayjsLocale(textLocale);

  const badge = useMemo(() => {
    if (!data) return { color: 'green' as const, text: t('dashboard.offlineStatusWidget.badge_healthy') };
    const pendingCount = data.totalPendingOrders + data.totalPendingTransactions;
    if (data.hasCriticalIssues) {
      return { color: 'red' as const, text: t('dashboard.offlineStatusWidget.badge_critical') };
    }
    if (pendingCount > 0) {
      return { color: 'orange' as const, text: t('dashboard.offlineStatusWidget.badge_pending') };
    }
    return { color: 'green' as const, text: t('dashboard.offlineStatusWidget.badge_healthy') };
  }, [data, t]);

  const handleRefresh = () => {
    void query.refetch();
    onRefresh?.();
  };

  if (query.isLoading && !data) {
    return (
      <WidgetShell title={title} dragHandleProps={dragHandleProps} onRefresh={handleRefresh}>
        <Statistic loading value={0} />
      </WidgetShell>
    );
  }

  if (!data) {
    return (
      <WidgetShell title={title} dragHandleProps={dragHandleProps} onRefresh={handleRefresh}>
        <span>{t('dashboard.offlineStatusWidget.load_failed')}</span>
      </WidgetShell>
    );
  }

  const pendingCount = data.totalPendingOrders + data.totalPendingTransactions;
  const syncHealth = data.syncHealth;

  return (
    <WidgetShell
      title={title}
      dragHandleProps={dragHandleProps}
      onRefresh={handleRefresh}
      refreshing={query.isFetching}
      extra={<Badge color={badge.color} text={badge.text} />}
    >
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(3, 1fr)',
            gap: 16,
          }}
        >
          <Statistic
            title={t('dashboard.offlineStatusWidget.pending_orders')}
            value={data.totalPendingOrders}
            prefix={
              data.totalPendingOrders > 0 ? <WarningOutlined /> : <CheckCircleOutlined />
            }
            styles={{ content: { color: pendingOrdersColor(data.totalPendingOrders) } }}
            loading={query.isLoading}
          />
          <Statistic
            title={t('dashboard.offlineStatusWidget.total_pending')}
            value={pendingCount}
            suffix={`/ ${OFFLINE_PENDING_ORDERS_CAP}`}
            loading={query.isLoading}
          />
          <Statistic
            title={t('dashboard.offlineStatusWidget.sync_success_rate')}
            value={syncHealth.successRate}
            suffix="%"
            styles={{ content: { color: successRateColor(syncHealth.successRate) } }}
            loading={query.isLoading}
          />
        </div>

        <div>
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              fontSize: 12,
              color: '#64748b',
              marginBottom: 4,
            }}
          >
            <span>{t('dashboard.offlineStatusWidget.sync_status')}</span>
            <span>
              {syncHealth.isHealthy
                ? t('dashboard.offlineStatusWidget.sync_healthy')
                : t('dashboard.offlineStatusWidget.sync_unhealthy')}
            </span>
          </div>
          <Progress
            percent={syncHealth.successRate}
            strokeColor={successRateColor(syncHealth.successRate)}
            size="small"
          />
        </div>

        {data.oldestPendingOrder ? (
          <div style={{ fontSize: 12, color: '#64748b' }}>
            {t('dashboard.offlineStatusWidget.oldest_pending', {
              time: dayjs(data.oldestPendingOrder).locale(relativeLocale).fromNow(),
            })}
          </div>
        ) : null}

        {data.lastSyncAt ? (
          <div style={{ fontSize: 12, color: '#64748b' }}>
            {t('dashboard.offlineStatusWidget.last_sync', {
              time: dayjs(data.lastSyncAt).locale(relativeLocale).fromNow(),
            })}
          </div>
        ) : null}

        <Link href="/rksv/offline-orders" style={{ display: 'block' }}>
          <Button type="primary" icon={<CloudSyncOutlined />} block>
            {t('dashboard.offlineStatusWidget.view_details')}
          </Button>
        </Link>
      </Space>
    </WidgetShell>
  );
}
