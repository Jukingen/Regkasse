'use client';

import { Alert, Button, Col, Row, Space, Statistic, Tag, Typography } from 'antd';
import Link from 'next/link';

import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { useDataRetentionStats } from '@/features/data-management/hooks/useDataRetentionStats';
import { usePermissions } from '@/hooks/usePermissions';
import { formatDate, useI18n } from '@/i18n';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'>;

/**
 * Dashboard widget: SuperAdmin sees platform retention/lifecycle counts;
 * Mandanten-Admin (backup.manage) sees own-tenant mapped stats.
 */
export function DataRetentionWidget({ title, dragHandleProps, onRefresh }: Props) {
  const { t, formatLocale } = useI18n();
  const { isSuperAdmin } = usePermissions();
  const query = useDataRetentionStats();

  const handleRefresh = () => {
    void query.refetch();
    onRefresh?.();
  };

  const detailsHref = isSuperAdmin ? '/admin/data-management' : '/settings/data-management';
  const stats = query.data;

  if (query.isLoading && !stats) {
    return (
      <WidgetShell title={title} dragHandleProps={dragHandleProps} onRefresh={handleRefresh}>
        <Statistic loading value={0} />
      </WidgetShell>
    );
  }

  if (!stats) {
    return (
      <WidgetShell title={title} dragHandleProps={dragHandleProps} onRefresh={handleRefresh}>
        <Typography.Text type="secondary">
          {t('dashboard.dataRetentionWidget.loadFailed')}
        </Typography.Text>
      </WidgetShell>
    );
  }

  return (
    <WidgetShell
      title={title}
      dragHandleProps={dragHandleProps}
      onRefresh={handleRefresh}
      extra={
        <Link href={detailsHref}>
          <Button type="link" size="small" style={{ paddingInline: 0 }}>
            {t('dashboard.dataRetentionWidget.viewAll')}
          </Button>
        </Link>
      }
    >
      <Row gutter={[16, 16]}>
        <Col xs={12} sm={6}>
          <Statistic
            title={
              isSuperAdmin
                ? t('dashboard.dataRetentionWidget.totalTenants')
                : t('dashboard.dataRetentionWidget.thisTenant')
            }
            value={stats.totalTenants}
          />
        </Col>
        <Col xs={12} sm={6}>
          <Statistic
            title={t('dashboard.dataRetentionWidget.inGrace')}
            value={stats.inGraceCount}
            valueStyle={stats.inGraceCount > 0 ? { color: '#eab308' } : undefined}
          />
        </Col>
        <Col xs={12} sm={6}>
          <Statistic
            title={t('dashboard.dataRetentionWidget.locked')}
            value={stats.lockedCount}
            valueStyle={stats.lockedCount > 0 ? { color: '#dc2626' } : undefined}
          />
        </Col>
        <Col xs={12} sm={6}>
          <Statistic
            title={t('dashboard.dataRetentionWidget.deletionRequests')}
            value={stats.pendingDeletionRequestCount}
            valueStyle={stats.pendingDeletionRequestCount > 0 ? { color: '#f59e0b' } : undefined}
          />
        </Col>
      </Row>

      <Space wrap style={{ marginTop: 12 }}>
        <Tag color="success">
          {t('dashboard.dataRetentionWidget.tagActive', { count: stats.activeCount })}
        </Tag>
        <Tag color="warning">
          {t('dashboard.dataRetentionWidget.tagGrace', { count: stats.inGraceCount })}
        </Tag>
        <Tag color="error">
          {t('dashboard.dataRetentionWidget.tagLocked', { count: stats.lockedCount })}
        </Tag>
      </Space>

      <Alert
        style={{ marginTop: 12 }}
        type="info"
        showIcon
        title={t('dashboard.dataRetentionWidget.rksvNote')}
      />

      {stats.oldestRksvData ? (
        <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8, fontSize: 12 }}>
          {t('dashboard.dataRetentionWidget.oldestRksv', {
            date: formatDate(stats.oldestRksvData, formatLocale),
          })}
        </Typography.Text>
      ) : null}
    </WidgetShell>
  );
}
