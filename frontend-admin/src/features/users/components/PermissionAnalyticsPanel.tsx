'use client';

import { DownloadOutlined } from '@ant-design/icons';
import {
  Alert,
  Button,
  Card,
  Col,
  Row,
  Space,
  Statistic,
  Table,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMutation, useQuery } from '@tanstack/react-query';

import {
  type PermissionAnalyticsNamedCountDto,
  exportPermissionAnalyticsPdf,
  fetchPermissionAnalyticsSummary,
} from '@/features/users/api/permissionAnalyticsApi';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

export function PermissionAnalyticsPanel() {
  const { t } = useI18n();
  const { message } = useAntdApp();

  const summaryQuery = useQuery({
    queryKey: ['permission-analytics', 'summary'],
    queryFn: fetchPermissionAnalyticsSummary,
  });

  const exportMutation = useMutation({
    mutationFn: exportPermissionAnalyticsPdf,
    onSuccess: (blob) => {
      downloadBlob(blob, 'permission-analytics.pdf');
      message.success(t('access.permissionStats.exportSuccess'));
    },
    onError: () => message.error(t('access.permissionStats.exportError')),
  });

  const summary = summaryQuery.data;
  const namedColumns: ColumnsType<PermissionAnalyticsNamedCountDto> = [
    {
      title: t('access.permissionStats.columnLabel'),
      dataIndex: 'label',
      render: (label: string, row) => label || row.key,
    },
    {
      title: t('access.permissionStats.columnUsers'),
      dataIndex: 'userCount',
      width: 100,
    },
    {
      title: t('access.permissionStats.columnPercent'),
      dataIndex: 'percent',
      width: 100,
      render: (p: number) => `${p.toFixed(1)}%`,
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
        <Button
          icon={<DownloadOutlined />}
          loading={exportMutation.isPending}
          onClick={() => exportMutation.mutate()}
        >
          {t('access.permissionStats.exportPdf')}
        </Button>
      </div>

      {summaryQuery.isError ? (
        <Alert type="error" showIcon title={t('access.permissionStats.loadError')} />
      ) : null}

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={8}>
          <Card size="small">
            <Statistic
              title={t('access.permissionStats.kpiUsers')}
              value={summary?.totalUsers ?? 0}
              loading={summaryQuery.isLoading}
            />
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card size="small">
            <Statistic
              title={t('access.permissionStats.kpiRoles')}
              value={summary?.totalRoles ?? 0}
              loading={summaryQuery.isLoading}
            />
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card size="small">
            <Statistic
              title={t('access.permissionStats.kpiPermissions')}
              value={summary?.totalPermissions ?? 0}
              loading={summaryQuery.isLoading}
            />
          </Card>
        </Col>
      </Row>

      {(summary?.recommendations ?? []).map((rec) => (
        <Alert
          key={`${rec.code}-${rec.message}`}
          type={
            rec.severity === 'error' || rec.severity === 'critical'
              ? 'error'
              : rec.severity === 'warning'
                ? 'warning'
                : 'info'
          }
          showIcon
          title={rec.message}
          description={rec.arg || undefined}
        />
      ))}

      <Row gutter={[16, 16]}>
        <Col xs={24} lg={12}>
          <Card title={t('access.permissionStats.mostUsed')} size="small">
            <Table
              rowKey="key"
              size="small"
              pagination={false}
              loading={summaryQuery.isLoading}
              dataSource={summary?.mostUsed ?? []}
              columns={namedColumns}
            />
          </Card>
        </Col>
        <Col xs={24} lg={12}>
          <Card title={t('access.permissionStats.leastUsed')} size="small">
            <Table
              rowKey="key"
              size="small"
              pagination={false}
              loading={summaryQuery.isLoading}
              dataSource={summary?.leastUsed ?? []}
              columns={namedColumns}
            />
          </Card>
        </Col>
      </Row>

      <Card title={t('access.permissionStats.roleDistribution')} size="small">
        <Table
          rowKey="key"
          size="small"
          loading={summaryQuery.isLoading}
          dataSource={summary?.roleDistribution ?? []}
          columns={namedColumns}
          pagination={{ pageSize: 10 }}
        />
      </Card>

      {(summary?.unusedPermissions?.length ?? 0) > 0 ? (
        <Card title={t('access.permissionStats.unusedPermissions')} size="small">
          <Space wrap>
            {(summary?.unusedPermissions ?? []).slice(0, 40).map((key) => (
              <Typography.Text key={key} code>
                {key}
              </Typography.Text>
            ))}
          </Space>
        </Card>
      ) : null}
    </div>
  );
}
