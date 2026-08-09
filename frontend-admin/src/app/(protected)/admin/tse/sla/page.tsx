'use client';

import { useMutation, useQuery } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  Progress,
  Row,
  Space,
  Statistic,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  TseActiveTenantTag,
  TseTenantRequiredAlert,
} from '@/features/tse-shared/components/TseTenantContextUi';
import { useTsePageTenant } from '@/features/tse-shared/hooks/useTsePageTenant';
import {
  checkTseSlaViolations,
  getTseSlaReport,
} from '@/features/tse-sla/api/sla';
import type { TseSlaReport, TseSlaViolation } from '@/features/tse-sla/types';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const SLA_KEY = ['admin', 'tse-sla'] as const;

function gradeColor(grade: string): string {
  return grade === 'A' ? 'green' : grade === 'B' ? 'blue' : grade === 'C' ? 'gold' : 'orange';
}

function metColor(met: boolean): string {
  return met ? '#52c41a' : '#cf1322';
}

export default function TseSlaPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const { tenantId, isReady } = useTsePageTenant();

  const reportQuery = useQuery({
    queryKey: [...SLA_KEY, 'report', tenantId],
    queryFn: ({ signal }) => getTseSlaReport(tenantId!, undefined, undefined, signal),
    enabled: allowed && !!tenantId,
  });

  const checkMutation = useMutation({
    mutationFn: () => checkTseSlaViolations(tenantId!),
    onSuccess: (alert) => {
      notify.success(t('tseSla.checkSuccess'));
      if (alert.hasViolations) {
        notify.warning(alert.message, { mode: 'notification' });
      }
      void reportQuery.refetch();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseSla.checkViolations',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const report: TseSlaReport | undefined = reportQuery.data;

  const violationColumns: ColumnsType<TseSlaViolation> = [
    {
      title: t('tseSla.colMetric'),
      dataIndex: 'metric',
      key: 'metric',
    },
    {
      title: t('tseSla.colSeverity'),
      dataIndex: 'severity',
      key: 'severity',
      render: (severity: string) => (
        <Tag color={severity === 'Critical' ? 'red' : 'orange'}>{severity}</Tag>
      ),
    },
    {
      title: t('tseSla.colMessage'),
      dataIndex: 'message',
      key: 'message',
    },
    {
      title: t('tseSla.colActual'),
      dataIndex: 'actualValue',
      key: 'actualValue',
    },
    {
      title: t('tseSla.colTarget'),
      dataIndex: 'targetValue',
      key: 'targetValue',
    },
  ];

  if (!allowed) {
    return <Alert type="error" showIcon title={t('tseSla.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseSla.title')}
        breadcrumbs={buildPlatformAdminBreadcrumbs(t, 'securityTse', { title: t('tseSla.title') })}
        extra={
          <Space>
            <TseActiveTenantTag />
            <Button
              disabled={!tenantId}
              loading={reportQuery.isFetching}
              onClick={() => void reportQuery.refetch()}
            >
              {t('tseSla.loadReport')}
            </Button>
            <Button
              type="primary"
              disabled={!tenantId}
              loading={checkMutation.isPending}
              onClick={() => checkMutation.mutate()}
            >
              {t('tseSla.checkViolations')}
            </Button>
          </Space>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('tseSla.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {!isReady ? (
        <TseTenantRequiredAlert emptySelectKey="tseSla.emptySelect" />
      ) : (
        <Card title={t('tseSla.cardTitle')} loading={reportQuery.isLoading}>
          {report ? (
            <>
              <Typography.Text type="secondary">{t('tseSla.periodHint')}</Typography.Text>
              <Row gutter={16} style={{ marginTop: 16 }}>
                <Col span={8}>
                  <Statistic
                    title={t('tseSla.uptime')}
                    value={report.uptimePercentage}
                    suffix="%"
                    valueStyle={{ color: metColor(report.isUptimeTargetMet) }}
                  />
                  <Progress
                    percent={Math.min(100, report.uptimePercentage)}
                    strokeColor={metColor(report.isUptimeTargetMet)}
                    format={() =>
                      `${report.uptimePercentage}% / ${report.targetUptimePercentage}%`
                    }
                  />
                </Col>
                <Col span={8}>
                  <Statistic
                    title={t('tseSla.responseTime')}
                    value={report.averageResponseTime}
                    suffix="ms"
                    valueStyle={{ color: metColor(report.isResponseTimeTargetMet) }}
                  />
                  <Typography.Text type="secondary">
                    {t('tseSla.responseTarget', { ms: report.targetResponseTime })}
                  </Typography.Text>
                </Col>
                <Col span={8}>
                  <Statistic
                    title={t('tseSla.successRate')}
                    value={report.successRate}
                    suffix="%"
                    valueStyle={{ color: metColor(report.isSuccessRateTargetMet) }}
                  />
                  <Progress
                    percent={Math.min(100, report.successRate)}
                    strokeColor={metColor(report.isSuccessRateTargetMet)}
                    format={() => `${report.successRate}% / ${report.targetSuccessRate}%`}
                  />
                </Col>
              </Row>

              <div style={{ marginTop: 16 }}>
                <Space>
                  <Tag color={gradeColor(report.grade)}>
                    {t('tseSla.grade')}: {report.grade}
                  </Tag>
                  <Tag color={report.violations.length === 0 ? 'green' : 'red'}>
                    {report.violations.length === 0
                      ? t('tseSla.compliant')
                      : t('tseSla.nonCompliant')}
                  </Tag>
                </Space>
              </div>

              <Typography.Title level={5} style={{ marginTop: 24 }}>
                {t('tseSla.violationsTitle')}
              </Typography.Title>
              <Table
                rowKey={(row) => `${row.code}-${row.detectedAt}`}
                columns={violationColumns}
                dataSource={report.violations}
                pagination={false}
                locale={{ emptyText: t('tseSla.noViolations') }}
              />
            </>
          ) : reportQuery.isError ? (
            <Alert type="error" showIcon title={t('tseSla.loadError')} />
          ) : null}
        </Card>
      )}
    </>
  );
}
