'use client';

import { useQuery } from '@tanstack/react-query';
import { Alert, Card, Col, List, Row, Space, Statistic, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useTranslation } from 'react-i18next';

import {
  getWeeklyTenantActivityReport,
  type ActivityAnomaly,
  type ActivitySummary,
} from '@/features/tenants/api/activityReport';
import { formatDateTime, useI18n } from '@/i18n';

type Props = {
  tenantId: string;
};

export function TenantActivityReport({ tenantId }: Props) {
  const { t } = useTranslation('tenants');
  const { formatLocale } = useI18n();

  const { data: report, isLoading, isError } = useQuery({
    queryKey: ['tenant-activity-report', tenantId],
    queryFn: () => getWeeklyTenantActivityReport(tenantId),
    enabled: Boolean(tenantId),
  });

  const columns: ColumnsType<ActivitySummary> = [
    {
      title: t('detail.activityReport.columns.operationType'),
      dataIndex: 'operationType',
      key: 'operationType',
    },
    {
      title: t('detail.activityReport.columns.count'),
      dataIndex: 'count',
      key: 'count',
      width: 100,
    },
    {
      title: t('detail.activityReport.columns.users'),
      dataIndex: 'users',
      key: 'users',
      width: 100,
    },
    {
      title: t('detail.activityReport.columns.firstOccurrence'),
      dataIndex: 'firstOccurrence',
      key: 'firstOccurrence',
      render: (value: string) => formatDateTime(value, formatLocale),
    },
    {
      title: t('detail.activityReport.columns.lastOccurrence'),
      dataIndex: 'lastOccurrence',
      key: 'lastOccurrence',
      render: (value: string) => formatDateTime(value, formatLocale),
    },
  ];

  const anomalyCount = report?.anomalies?.length ?? 0;

  return (
    <Card title={t('detail.activityReport.title')} loading={isLoading}>
      {isError ? (
        <Alert type="error" showIcon message={t('detail.activityReport.loadError')} />
      ) : (
        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
          {report?.period ? (
            <Typography.Text type="secondary">
              {t('detail.activityReport.period', {
                from: formatDateTime(report.period.fromUtc, formatLocale),
                to: formatDateTime(report.period.toUtc, formatLocale),
              })}
            </Typography.Text>
          ) : null}

          <Row gutter={[16, 16]}>
            <Col xs={24} sm={8}>
              <Statistic
                title={t('detail.activityReport.totalActivities')}
                value={report?.totalActivities ?? 0}
              />
            </Col>
            <Col xs={24} sm={8}>
              <Statistic
                title={t('detail.activityReport.uniqueUsers')}
                value={report?.uniqueUsers ?? 0}
              />
            </Col>
            <Col xs={24} sm={8}>
              <Statistic
                title={t('detail.activityReport.anomaliesDetected')}
                value={anomalyCount}
                valueStyle={{ color: anomalyCount > 0 ? '#cf1322' : '#3f8600' }}
              />
            </Col>
          </Row>

          <Table<ActivitySummary>
            size="small"
            rowKey="operationType"
            dataSource={report?.activitySummary ?? []}
            columns={columns}
            pagination={false}
            locale={{ emptyText: t('detail.activityReport.emptySummary') }}
          />

          {anomalyCount > 0 ? (
            <Card type="inner" title={t('detail.activityReport.anomaliesTitle')}>
              <List
                dataSource={report?.anomalies ?? []}
                renderItem={(anomaly: ActivityAnomaly) => (
                  <List.Item>
                    <Alert
                      style={{ width: '100%' }}
                      type="error"
                      showIcon
                      message={anomaly.description}
                      description={anomaly.recommendation}
                    />
                  </List.Item>
                )}
              />
            </Card>
          ) : null}

          {(report?.recommendations?.length ?? 0) > 0 ? (
            <Card type="inner" title={t('detail.activityReport.recommendationsTitle')}>
              <List
                size="small"
                dataSource={report?.recommendations ?? []}
                renderItem={(item) => <List.Item>{item}</List.Item>}
              />
            </Card>
          ) : null}
        </Space>
      )}
    </Card>
  );
}
