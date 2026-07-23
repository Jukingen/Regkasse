'use client';

import { useQuery } from '@tanstack/react-query';
import { Alert, Card, Col, Row, Select, Space, Statistic, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo, useState } from 'react';

import { dateColumnRender } from '@/components/DateColumn';
import {
  getApprovalHistory,
  getApprovalHistoryReport,
  type ApprovalHistoryReport,
  type ApprovalRequestDto,
} from '@/features/admin/api/approvals';
import { useI18n } from '@/i18n';

const HISTORY_QUERY_KEY = ['admin', 'approvals', 'history'] as const;
const REPORT_QUERY_KEY = ['admin', 'approvals', 'history-report'] as const;

function statusColor(status: string): string {
  const s = status.toLowerCase();
  if (s === 'approved' || s === 'consumed') return 'green';
  if (s === 'rejected') return 'red';
  if (s === 'expired') return 'default';
  return 'orange';
}

type Props = {
  enabled?: boolean;
};

export function ApprovalHistoryPanel({ enabled = true }: Props) {
  const { t } = useI18n();
  const [statusFilter, setStatusFilter] = useState<string | undefined>();

  const historyQuery = useQuery({
    queryKey: [...HISTORY_QUERY_KEY, statusFilter],
    queryFn: () =>
      getApprovalHistory({
        status: statusFilter,
        limit: 100,
      }),
    enabled,
  });

  const reportQuery = useQuery({
    queryKey: REPORT_QUERY_KEY,
    queryFn: () => getApprovalHistoryReport(),
    enabled,
  });

  const report: ApprovalHistoryReport | undefined = reportQuery.data;
  const rows = historyQuery.data ?? [];

  const columns: ColumnsType<ApprovalRequestDto> = useMemo(
    () => [
      {
        title: t('common.approvals.columns.date'),
        dataIndex: 'requestedAt',
        key: 'requestedAt',
        render: dateColumnRender('datetimeSeconds'),
      },
      {
        title: t('common.approvals.columns.action'),
        dataIndex: 'actionType',
        key: 'actionType',
        render: (type: string) => <Tag>{type}</Tag>,
      },
      {
        title: t('common.approvals.columns.requestedBy'),
        key: 'requestedBy',
        ellipsis: true,
        render: (_: unknown, row) =>
          row.requestedByEmail ||
          row.requestedByDisplayName ||
          row.requestedBy ||
          t('common.approvals.unknownUser'),
      },
      {
        title: t('common.approvals.columns.approvedBy'),
        key: 'approvedBy',
        ellipsis: true,
        render: (_: unknown, row) =>
          row.approvedByEmail ||
          row.approvedByDisplayName ||
          row.approvedBy ||
          '—',
      },
      {
        title: t('common.approvals.columns.status'),
        dataIndex: 'status',
        key: 'status',
        render: (status: string) => <Tag color={statusColor(status)}>{status}</Tag>,
      },
      {
        title: t('common.approvals.columns.timeToApproval'),
        key: 'timeToApproval',
        render: (_: unknown, record) => {
          if (record.timeToDecisionMinutes == null) return '—';
          return t('common.approvals.timeToApprovalValue', {
            count: record.timeToDecisionMinutes,
          });
        },
      },
    ],
    [t]
  );

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Card title={t('common.approvals.reportTitle')} loading={reportQuery.isLoading}>
        {reportQuery.isError ? (
          <Alert type="error" showIcon title={t('common.approvals.reportLoadFailed')} />
        ) : (
          <>
            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 16 }}>
              {t('common.approvals.reportPeriodHint')}
            </Typography.Text>
            <Row gutter={[16, 16]}>
              <Col xs={12} sm={8} md={4}>
                <Statistic title={t('common.approvals.stats.total')} value={report?.totalRequests ?? 0} />
              </Col>
              <Col xs={12} sm={8} md={4}>
                <Statistic title={t('common.approvals.stats.pending')} value={report?.pendingCount ?? 0} />
              </Col>
              <Col xs={12} sm={8} md={4}>
                <Statistic title={t('common.approvals.stats.approved')} value={report?.approvedCount ?? 0} />
              </Col>
              <Col xs={12} sm={8} md={4}>
                <Statistic title={t('common.approvals.stats.rejected')} value={report?.rejectedCount ?? 0} />
              </Col>
              <Col xs={12} sm={8} md={4}>
                <Statistic
                  title={t('common.approvals.stats.avgMinutes')}
                  value={report?.averageTimeToApprovalMinutes ?? 0}
                  precision={1}
                  suffix={t('common.approvals.minutesShort')}
                />
              </Col>
              <Col xs={12} sm={8} md={4}>
                <Statistic
                  title={t('common.approvals.stats.medianMinutes')}
                  value={report?.medianTimeToApprovalMinutes ?? 0}
                  precision={1}
                  suffix={t('common.approvals.minutesShort')}
                />
              </Col>
            </Row>
          </>
        )}
      </Card>

      <Card
        title={t('common.approvals.historyTitle')}
        extra={
          <Select
            allowClear
            placeholder={t('common.approvals.statusFilter')}
            style={{ minWidth: 160 }}
            value={statusFilter}
            onChange={(value) => setStatusFilter(value)}
            options={[
              { value: 'Pending', label: t('common.approvals.status.pending') },
              { value: 'Approved', label: t('common.approvals.status.approved') },
              { value: 'Rejected', label: t('common.approvals.status.rejected') },
              { value: 'Expired', label: t('common.approvals.status.expired') },
              { value: 'Consumed', label: t('common.approvals.status.consumed') },
            ]}
          />
        }
      >
        {historyQuery.isError ? (
          <Alert type="error" showIcon title={t('common.approvals.historyLoadFailed')} />
        ) : (
          <Table
            dataSource={rows}
            rowKey="id"
            loading={historyQuery.isLoading}
            columns={columns}
            pagination={{ pageSize: 20 }}
            locale={{ emptyText: t('common.approvals.historyEmpty') }}
          />
        )}
      </Card>
    </Space>
  );
}
