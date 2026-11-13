'use client';

import { DownloadOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import {
  Button,
  Card,
  Col,
  DatePicker,
  Empty,
  Row,
  Space,
  Statistic,
  Table,
  Tabs,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
import React, { useMemo, useState } from 'react';
import {
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  downloadTaxReportCsv,
  getTaxReport,
  getTaxTrend,
  taxReportQueryKey,
  taxTrendQueryKey,
  type TaxGroupSummary,
} from '@/features/tax/api/taxReports';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { formatCurrency } from '@/i18n/formatting';
import { DAYJS_DATE_FORMAT, formatUserMonthDay } from '@/lib/dateFormatter';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';

dayjs.extend(utc);

const { RangePicker } = DatePicker;
const PIE_COLORS = ['#1677ff', '#52c41a', '#faad14', '#722ed1', '#13c2c2', '#eb2f96'];

function triggerBlobDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}

export default function SteuerberichtePage() {
  const { t, formatLocale } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const canExport = hasPermission(PERMISSIONS.REPORT_EXPORT);

  const [range, setRange] = useState<[Dayjs, Dayjs]>([
    dayjs().startOf('month'),
    dayjs().endOf('month'),
  ]);
  const [exporting, setExporting] = useState<'year' | 'month' | null>(null);

  const fromUtc = range[0].startOf('day').utc().toISOString();
  const toUtc = range[1].add(1, 'day').startOf('day').utc().toISOString();

  const reportQuery = useQuery({
    queryKey: [...taxReportQueryKey, fromUtc, toUtc],
    queryFn: () => getTaxReport({ fromUtc, toUtc }),
  });

  const trendQuery = useQuery({
    queryKey: [...taxTrendQueryKey, fromUtc, toUtc, 'day'],
    queryFn: () => getTaxTrend({ fromUtc, toUtc, granularity: 'day' }),
  });

  const money = (value: number) => formatCurrency(value, formatLocale, { currency: 'EUR' });

  const distribution = useMemo(
    () =>
      (reportQuery.data?.taxGroups ?? []).map((g) => ({
        name: `${g.taxGroupName} (${g.rate}%)`,
        value: Number(g.taxAmount),
        rate: g.rate,
      })),
    [reportQuery.data]
  );

  const trendChart = useMemo(() => {
    const points = trendQuery.data ?? [];
    const labels = [...new Set(points.map((p) => p.taxRateLabel))];
    const byDate = new Map<string, Record<string, string | number>>();
    for (const p of points) {
      const key = p.date.slice(0, 10);
      const row = byDate.get(key) ?? { date: key };
      row[p.taxRateLabel] = Number(p.amount);
      byDate.set(key, row);
    }
    const data = [...byDate.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([, row]) => ({
        ...row,
        dateLabel: formatUserMonthDay(String(row.date)),
      }));
    return { data, labels };
  }, [trendQuery.data]);

  const columns: ColumnsType<TaxGroupSummary> = [
    { title: t('reporting.taxReports.columns.group'), dataIndex: 'taxGroupName', key: 'name' },
    {
      title: t('reporting.taxReports.columns.rate'),
      dataIndex: 'rate',
      key: 'rate',
      render: (v: number) => `${v}%`,
    },
    {
      title: t('reporting.taxReports.columns.net'),
      dataIndex: 'netRevenue',
      key: 'net',
      align: 'right',
      render: (v: number) => money(v),
    },
    {
      title: t('reporting.taxReports.columns.tax'),
      dataIndex: 'taxAmount',
      key: 'tax',
      align: 'right',
      render: (v: number) => money(v),
    },
    {
      title: t('reporting.taxReports.columns.gross'),
      dataIndex: 'grossRevenue',
      key: 'gross',
      align: 'right',
      render: (v: number) => money(v),
    },
    {
      title: t('reporting.taxReports.columns.receipts'),
      dataIndex: 'transactionCount',
      key: 'count',
      align: 'right',
    },
  ];

  const handleExport = async (period: 'year' | 'month') => {
    setExporting(period);
    try {
      const blob = await downloadTaxReportCsv({ period });
      triggerBlobDownload(
        blob,
        period === 'year'
          ? `jahressteuerbericht_${dayjs().format('YYYY')}.csv`
          : `monatssteuerbericht_${dayjs().format('YYYY-MM')}.csv`
      );
      notify.successKey('reporting.taxReports.exportSuccess');
    } catch (err) {
      notify.apiError(err, {
        logContext: 'TaxReports.export',
        fallbackKey: 'reporting.taxReports.exportFailed',
      });
    } finally {
      setExporting(null);
    }
  };

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.operationalReports'), href: '/reporting' },
    { title: t('reporting.taxReports.pageTitle') },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader title={t('reporting.taxReports.pageTitle')} breadcrumbs={breadcrumbs} />

      <Card>
        <Space wrap style={{ marginBottom: 16 }}>
          <Typography.Text>{t('reporting.taxReports.period')}</Typography.Text>
          <RangePicker
            value={range}
            format={DAYJS_DATE_FORMAT}
            onChange={(values) => {
              if (values?.[0] && values[1]) setRange([values[0], values[1]]);
            }}
          />
        </Space>

        <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
          <Col xs={24} md={8}>
            <Statistic
              title={t('reporting.taxReports.totalNet')}
              value={reportQuery.data?.totalNetRevenue ?? 0}
              formatter={(v) => money(Number(v))}
              loading={reportQuery.isLoading}
            />
          </Col>
          <Col xs={24} md={8}>
            <Statistic
              title={t('reporting.taxReports.totalTax')}
              value={reportQuery.data?.totalTaxAmount ?? 0}
              formatter={(v) => money(Number(v))}
              loading={reportQuery.isLoading}
            />
          </Col>
          <Col xs={24} md={8}>
            <Statistic
              title={t('reporting.taxReports.totalGross')}
              value={reportQuery.data?.totalGrossRevenue ?? 0}
              formatter={(v) => money(Number(v))}
              loading={reportQuery.isLoading}
            />
          </Col>
        </Row>

        <Card title={t('reporting.taxReports.cardTitle')} variant="borderless">
          <Tabs
            items={[
              {
                key: 'distribution',
                label: t('reporting.taxReports.tabs.distribution'),
                children: (
                  <div>
                    {distribution.length === 0 ? (
                      <Empty description={t('reporting.taxReports.empty')} />
                    ) : (
                      <ResponsiveContainer width="100%" height={320}>
                        <PieChart>
                          <Pie
                            data={distribution}
                            dataKey="value"
                            nameKey="name"
                            cx="50%"
                            cy="50%"
                            outerRadius={110}
                            label={({ name, percent }) =>
                              `${name} ${((percent ?? 0) * 100).toFixed(0)}%`
                            }
                          >
                            {distribution.map((_, i) => (
                              <Cell key={i} fill={PIE_COLORS[i % PIE_COLORS.length]} />
                            ))}
                          </Pie>
                          <Tooltip formatter={(value) => money(Number(value))} />
                          <Legend />
                        </PieChart>
                      </ResponsiveContainer>
                    )}
                    <Table
                      style={{ marginTop: 16 }}
                      rowKey={(r) => `${r.taxGroupName}-${r.rate}`}
                      columns={columns}
                      dataSource={reportQuery.data?.taxGroups ?? []}
                      loading={reportQuery.isLoading}
                      pagination={false}
                      size="small"
                    />
                  </div>
                ),
              },
              {
                key: 'trend',
                label: t('reporting.taxReports.tabs.trend'),
                children:
                  trendChart.data.length === 0 ? (
                    <Empty description={t('reporting.taxReports.empty')} />
                  ) : (
                    <ResponsiveContainer width="100%" height={360}>
                      <LineChart data={trendChart.data} margin={{ top: 8, right: 16, left: 0, bottom: 0 }}>
                        <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.06)" />
                        <XAxis dataKey="dateLabel" tick={{ fontSize: 11 }} />
                        <YAxis tick={{ fontSize: 11 }} />
                        <Tooltip formatter={(value) => money(Number(value))} />
                        <Legend />
                        {trendChart.labels.map((label, i) => (
                          <Line
                            key={label}
                            type="monotone"
                            dataKey={label}
                            name={label}
                            stroke={PIE_COLORS[i % PIE_COLORS.length]}
                            strokeWidth={2}
                            dot={false}
                          />
                        ))}
                      </LineChart>
                    </ResponsiveContainer>
                  ),
              },
              {
                key: 'export',
                label: t('reporting.taxReports.tabs.export'),
                children: (
                  <Space wrap>
                    <Button
                      type="primary"
                      icon={<DownloadOutlined />}
                      disabled={!canExport}
                      loading={exporting === 'year'}
                      onClick={() => void handleExport('year')}
                    >
                      {t('reporting.taxReports.exportYear')}
                    </Button>
                    <Button
                      icon={<DownloadOutlined />}
                      disabled={!canExport}
                      loading={exporting === 'month'}
                      onClick={() => void handleExport('month')}
                    >
                      {t('reporting.taxReports.exportMonth')}
                    </Button>
                    {!canExport ? (
                      <Typography.Text type="secondary">
                        {t('reporting.taxReports.exportPermissionHint')}
                      </Typography.Text>
                    ) : null}
                  </Space>
                ),
              },
            ]}
          />
        </Card>
      </Card>
    </div>
  );
}
