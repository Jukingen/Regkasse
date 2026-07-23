'use client';

import {
  BarChartOutlined,
  FilePdfOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import {
  Alert,
  Button,
  Card,
  Col,
  Empty,
  List,
  Radio,
  Row,
  Space,
  Statistic,
  Table,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useMemo, useState } from 'react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import {
  type DownloadAnalyticsSlowExport,
  type DownloadHistoryAnalytics,
  useDownloadHistoryAnalytics,
} from '@/features/download-history/api/downloadHistoryAnalyticsApi';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { formatBytes, formatDateTime } from '@/i18n/formatting';
import { useAuth } from '@/features/auth/hooks/useAuth';

type TrendMode = 'daily' | 'weekly' | 'monthly';

function buildReportText(data: DownloadHistoryAnalytics, t: (k: string) => string): string {
  const lines: string[] = [
    t('common.downloadAnalytics.reportTitle'),
    `Total: ${data.totalCount}`,
    `Today: ${data.todayCount}`,
    `Month: ${data.monthCount}`,
    `Bytes: ${data.totalBytes}`,
    '',
    t('common.downloadAnalytics.topKindsTitle'),
    ...data.topKinds.map((k, i) => `${i + 1}. ${k.label} → ${k.count} (${k.percent}%)`),
    '',
    t('common.downloadAnalytics.topUsersTitle'),
    ...data.topUsers.map((u, i) => `${i + 1}. ${u.displayName} → ${u.count}`),
  ];

  if (data.topTenants.length > 0) {
    lines.push('', t('common.downloadAnalytics.topTenantsTitle'));
    lines.push(
      ...data.topTenants.map((ten, i) => `${i + 1}. ${ten.tenantName} → ${ten.count} (${ten.percent}%)`)
    );
  }

  if (data.slowExports.length > 0) {
    lines.push('', t('common.downloadAnalytics.slowTitle'));
    lines.push(
      ...data.slowExports.map(
        (s, i) =>
          `${i + 1}. ${s.fileName} | ${s.durationMs ?? '-'} ms | ${s.fileSize ?? '-'} B | ${s.displayName}`
      )
    );
  }

  return lines.join('\n');
}

function openPrintableReport(title: string, body: string): void {
  const w = window.open('', '_blank', 'noopener,noreferrer,width=900,height=700');
  if (!w) return;
  w.document.write(`<!doctype html><html><head><title>${title}</title>
    <style>
      body { font-family: system-ui, sans-serif; padding: 24px; color: #111; }
      h1 { font-size: 18px; }
      pre { white-space: pre-wrap; font-size: 12px; line-height: 1.45; }
      @media print { button { display: none; } }
    </style></head><body>
    <button onclick="window.print()">${title}</button>
    <h1>${title}</h1>
    <pre>${body.replace(/</g, '&lt;')}</pre>
    </body></html>`);
  w.document.close();
}

type Props = {
  /** Compact embed without page chrome. */
  embedded?: boolean;
};

/**
 * Download / export usage analytics: totals, popularity, trends, slow exports.
 */
export function DownloadAnalyticsDashboard({ embedded = false }: Props) {
  const { t, formatLocale } = useI18n();
  const notify = useNotify();
  const { user } = useAuth();
  const isSuperAdminUser = isSuperAdmin(user?.role);
  const [platform, setPlatform] = useState(false);
  const [trendMode, setTrendMode] = useState<TrendMode>('daily');
  const { data, isLoading, isError, refetch, isFetching } = useDownloadHistoryAnalytics(
    platform && isSuperAdminUser
  );

  const trendData = useMemo(() => {
    if (!data) return [];
    if (trendMode === 'weekly') return data.weeklyTrend;
    if (trendMode === 'monthly') return data.monthlyTrend;
    return data.dailyTrend;
  }, [data, trendMode]);

  const slowColumns: ColumnsType<DownloadAnalyticsSlowExport> = useMemo(
    () => [
      {
        title: t('common.downloadAnalytics.slowFile'),
        dataIndex: 'fileName',
        key: 'fileName',
        ellipsis: true,
      },
      {
        title: t('common.downloadAnalytics.slowDuration'),
        key: 'duration',
        width: 120,
        render: (_, row) =>
          row.durationMs != null
            ? t('common.downloadAnalytics.durationMs', { ms: row.durationMs })
            : '—',
      },
      {
        title: t('common.downloadAnalytics.slowSize'),
        key: 'size',
        width: 110,
        render: (_, row) =>
          row.fileSize != null ? formatBytes(row.fileSize, formatLocale) : '—',
      },
      {
        title: t('common.downloadAnalytics.slowUser'),
        dataIndex: 'displayName',
        key: 'user',
        width: 140,
        ellipsis: true,
      },
      {
        title: t('common.downloadAnalytics.slowWhen'),
        key: 'when',
        width: 160,
        render: (_, row) => formatDateTime(row.downloadedAt, formatLocale),
      },
    ],
    [formatLocale, t]
  );

  const handleDetailedReport = () => {
    if (!data) return;
    const text = buildReportText(data, t);
    const blob = new Blob([text], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `download-analytics_${new Date().toISOString().slice(0, 10)}.txt`;
    a.click();
    URL.revokeObjectURL(url);
    notify.success(t('common.downloadAnalytics.reportDownloaded'));
  };

  const handlePdf = () => {
    if (!data) return;
    openPrintableReport(t('common.downloadAnalytics.reportTitle'), buildReportText(data, t));
  };

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      {!embedded ? (
        <Space wrap style={{ width: '100%', justifyContent: 'space-between' }}>
          <Typography.Title level={4} style={{ margin: 0 }}>
            <BarChartOutlined style={{ marginRight: 8 }} />
            {t('common.downloadAnalytics.title')}
          </Typography.Title>
          <Space wrap>
            {isSuperAdminUser ? (
              <Radio.Group
                size="small"
                value={platform ? 'platform' : 'tenant'}
                onChange={(e) => setPlatform(e.target.value === 'platform')}
                optionType="button"
                options={[
                  { value: 'tenant', label: t('common.downloadAnalytics.scopeTenant') },
                  { value: 'platform', label: t('common.downloadAnalytics.scopePlatform') },
                ]}
              />
            ) : null}
            <Button
              icon={<ReloadOutlined />}
              loading={isFetching}
              onClick={() => void refetch()}
              size="small"
            >
              {t('common.buttons.refresh')}
            </Button>
            <Link href="/admin/download-history">{t('common.downloadAnalytics.backToHistory')}</Link>
          </Space>
        </Space>
      ) : null}

      {isError ? (
        <Alert type="error" showIcon title={t('common.downloadAnalytics.loadFailed')} />
      ) : null}

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={8}>
          <Card loading={isLoading} size="small">
            <Statistic
              title={t('common.downloadAnalytics.total')}
              value={data?.totalCount ?? 0}
            />
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card loading={isLoading} size="small">
            <Statistic title={t('common.downloadAnalytics.today')} value={data?.todayCount ?? 0} />
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card loading={isLoading} size="small">
            <Statistic title={t('common.downloadAnalytics.month')} value={data?.monthCount ?? 0} />
          </Card>
        </Col>
      </Row>

      <Row gutter={[16, 16]}>
        <Col xs={24} lg={12}>
          <Card size="small" title={t('common.downloadAnalytics.topKindsTitle')} loading={isLoading}>
            {!data?.topKinds.length ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} />
            ) : (
              <List
                size="small"
                dataSource={data.topKinds}
                renderItem={(item, index) => (
                  <List.Item>
                    <Typography.Text>
                      {index + 1}. {item.label}{' '}
                      <Typography.Text type="secondary">
                        → {item.count} {t('common.downloadAnalytics.times')} ({item.percent}%)
                      </Typography.Text>
                    </Typography.Text>
                  </List.Item>
                )}
              />
            )}
          </Card>
        </Col>
        <Col xs={24} lg={12}>
          <Card size="small" title={t('common.downloadAnalytics.topUsersTitle')} loading={isLoading}>
            {!data?.topUsers.length ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} />
            ) : (
              <List
                size="small"
                dataSource={data.topUsers}
                renderItem={(item, index) => (
                  <List.Item>
                    <Typography.Text>
                      {index + 1}. {item.displayName}{' '}
                      <Typography.Text type="secondary">
                        → {item.count} {t('common.downloadAnalytics.exports')}
                      </Typography.Text>
                    </Typography.Text>
                  </List.Item>
                )}
              />
            )}
          </Card>
        </Col>
      </Row>

      {data?.includesPlatformTenants && data.topTenants.length > 0 ? (
        <Card size="small" title={t('common.downloadAnalytics.topTenantsTitle')} loading={isLoading}>
          <List
            size="small"
            dataSource={data.topTenants}
            renderItem={(item, index) => (
              <List.Item>
                <Typography.Text>
                  {index + 1}. {item.tenantName}{' '}
                  <Typography.Text type="secondary">({item.tenantSlug})</Typography.Text>{' '}
                  <Typography.Text type="secondary">
                    → {item.count} ({item.percent}%)
                  </Typography.Text>
                </Typography.Text>
              </List.Item>
            )}
          />
        </Card>
      ) : null}

      <Card
        size="small"
        title={t('common.downloadAnalytics.trendTitle')}
        loading={isLoading}
        extra={
          <Radio.Group
            size="small"
            value={trendMode}
            onChange={(e) => setTrendMode(e.target.value)}
            optionType="button"
            options={[
              { value: 'daily', label: t('common.downloadAnalytics.trendDaily') },
              { value: 'weekly', label: t('common.downloadAnalytics.trendWeekly') },
              { value: 'monthly', label: t('common.downloadAnalytics.trendMonthly') },
            ]}
          />
        }
      >
        {trendData.every((p) => p.count === 0) ? (
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} />
        ) : (
          <ResponsiveContainer width="100%" height={240}>
            <BarChart data={trendData} margin={{ top: 8, right: 8, left: 0, bottom: 4 }}>
              <CartesianGrid strokeDasharray="3 3" vertical={false} />
              <XAxis dataKey="label" tick={{ fontSize: 11 }} interval="preserveStartEnd" />
              <YAxis allowDecimals={false} tick={{ fontSize: 11 }} width={36} />
              <Tooltip />
              <Bar dataKey="count" name={t('common.downloadAnalytics.exports')} fill="#1677ff" />
            </BarChart>
          </ResponsiveContainer>
        )}
      </Card>

      <Card
        size="small"
        title={t('common.downloadAnalytics.slowTitle')}
        loading={isLoading}
        extra={
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('common.downloadAnalytics.slowHint')}
          </Typography.Text>
        }
      >
        <Table
          size="small"
          rowKey="id"
          pagination={false}
          columns={slowColumns}
          dataSource={data?.slowExports ?? []}
          locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} /> }}
        />
      </Card>

      <Space wrap>
        <Button icon={<BarChartOutlined />} onClick={handleDetailedReport} disabled={!data}>
          {t('common.downloadAnalytics.detailedReport')}
        </Button>
        <Button icon={<FilePdfOutlined />} onClick={handlePdf} disabled={!data}>
          {t('common.downloadAnalytics.createPdf')}
        </Button>
      </Space>
    </Space>
  );
}
