'use client';

import { DownloadOutlined } from '@ant-design/icons';
import { Button, Card, Col, Row, Segmented, Skeleton, Statistic, Tabs, Typography } from 'antd';
import dayjs from 'dayjs';
import isoWeek from 'dayjs/plugin/isoWeek';
import utc from 'dayjs/plugin/utc';
import dynamic from 'next/dynamic';
import React, { useMemo, useState } from 'react';

import {
  downloadLicenseDashboardReportCsv,
  getLicenseDashboardActivationSeries,
  licenseDashboardQueryKeys,
} from '@/api/manual/adminLicense';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useLicenseDashboardStats } from '@/features/license/api/licenseStats';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { useNotify } from '@/hooks/useNotify';
import { formatDate, useI18n } from '@/i18n';

dayjs.extend(utc);
dayjs.extend(isoWeek);

const LicenseUsageTrendChart = dynamic(
  () => import('@/features/license/components/LicenseUsageTrendChart'),
  {
    ssr: false,
    loading: () => <Skeleton active paragraph={{ rows: 6 }} />,
  }
);

function formatPeriodLabel(iso: string, granularity: 'day' | 'week', formatLocale: string): string {
  const d = dayjs.utc(iso);
  if (!d.isValid()) return iso;
  if (granularity === 'day') {
    return formatDate(iso, formatLocale, { month: '2-digit', day: '2-digit' });
  }
  return `${d.format('DD.MM.')} (KW ${d.isoWeek()})`;
}

/**
 * Super Admin license usage analytics: overview KPIs, activation trend, CSV export.
 */
export function LicenseUsageAnalyticsCard() {
  const { t, formatLocale } = useI18n();
  const notify = useNotify();
  const { user } = useAuth();
  const isSuperAdminUser = isSuperAdmin(user?.role);
  const [granularity, setGranularity] = useState<'day' | 'week'>('day');
  const [exporting, setExporting] = useState(false);
  const lookbackDays = 30;

  const statsQuery = useLicenseDashboardStats({ enabled: isSuperAdminUser });
  const seriesQuery = useAuthorizedQuery({
    queryKey: licenseDashboardQueryKeys.series(granularity, lookbackDays),
    queryFn: () =>
      getLicenseDashboardActivationSeries({
        granularity,
        lookbackDays,
      }),
    requiredRole: 'SuperAdmin',
    enabled: isSuperAdminUser,
  });

  const trendData = useMemo(() => {
    const pts = seriesQuery.data?.points ?? [];
    return pts.map((p) => ({
      name: formatPeriodLabel(p.periodStartUtc, granularity, formatLocale),
      count: p.count,
    }));
  }, [seriesQuery.data?.points, granularity, formatLocale]);

  if (!isSuperAdminUser) return null;

  const stats = statsQuery.data;
  const exportReport = async () => {
    setExporting(true);
    try {
      await downloadLicenseDashboardReportCsv();
      notify.successKey('license.usageAnalytics.exportSuccess');
    } catch {
      notify.errorKey('license.usageAnalytics.exportFailed');
    } finally {
      setExporting(false);
    }
  };

  return (
    <Card title={t('license.usageAnalytics.title')} style={{ marginTop: 16, marginBottom: 16 }}>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        {t('license.usageAnalytics.subtitle')}
      </Typography.Paragraph>

      <Tabs
        items={[
          {
            key: 'overview',
            label: t('license.usageAnalytics.tabs.overview'),
            children: statsQuery.isLoading ? (
              <Skeleton active paragraph={{ rows: 3 }} />
            ) : statsQuery.isError ? (
              <Typography.Text type="danger">
                {t('license.usageAnalytics.loadFailed')}
              </Typography.Text>
            ) : (
              <Row gutter={[16, 16]}>
                <Col xs={24} sm={12} lg={6}>
                  <Statistic
                    title={t('license.usageAnalytics.kpi.totalTenants')}
                    value={stats?.totalTenants ?? 0}
                  />
                </Col>
                <Col xs={24} sm={12} lg={6}>
                  <Statistic
                    title={t('license.usageAnalytics.kpi.active')}
                    value={stats?.activeTenantLicenses ?? 0}
                    valueStyle={{ color: '#52c41a' }}
                  />
                </Col>
                <Col xs={24} sm={12} lg={6}>
                  <Statistic
                    title={t('license.usageAnalytics.kpi.grace')}
                    value={stats?.graceTenantLicenses ?? 0}
                    valueStyle={{ color: '#faad14' }}
                  />
                </Col>
                <Col xs={24} sm={12} lg={6}>
                  <Statistic
                    title={t('license.usageAnalytics.kpi.expired')}
                    value={stats?.lockedTenantLicenses ?? stats?.expiredTenantLicenses ?? 0}
                    valueStyle={{ color: '#cf1322' }}
                  />
                </Col>
              </Row>
            ),
          },
          {
            key: 'trend',
            label: t('license.usageAnalytics.tabs.trend'),
            children: (
              <>
                <div style={{ marginBottom: 12, textAlign: 'right' }}>
                  <Segmented
                    size="small"
                    value={granularity}
                    onChange={(v) => setGranularity(v as 'day' | 'week')}
                    options={[
                      { label: t('license.usageAnalytics.chartDay'), value: 'day' },
                      { label: t('license.usageAnalytics.chartWeek'), value: 'week' },
                    ]}
                  />
                </div>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                  {t('license.usageAnalytics.trendHint')}
                </Typography.Paragraph>
                {seriesQuery.isLoading ? (
                  <Skeleton active paragraph={{ rows: 6 }} />
                ) : seriesQuery.isError ? (
                  <Typography.Text type="danger">
                    {t('license.usageAnalytics.loadFailed')}
                  </Typography.Text>
                ) : trendData.length === 0 ? (
                  <Typography.Text type="secondary">
                    {t('license.usageAnalytics.trendEmpty')}
                  </Typography.Text>
                ) : (
                  <LicenseUsageTrendChart data={trendData} />
                )}
              </>
            ),
          },
          {
            key: 'export',
            label: t('license.usageAnalytics.tabs.export'),
            children: (
              <div>
                <Typography.Paragraph type="secondary">
                  {t('license.usageAnalytics.exportHint')}
                </Typography.Paragraph>
                <Button
                  type="primary"
                  icon={<DownloadOutlined />}
                  loading={exporting}
                  onClick={() => void exportReport()}
                >
                  {t('license.usageAnalytics.exportButton')}
                </Button>
              </div>
            ),
          },
        ]}
      />
    </Card>
  );
}
