'use client';

import { DownloadOutlined, FileTextOutlined } from '@ant-design/icons';
import { Badge, Button, Card, Col, Progress, Row, Space, Typography } from 'antd';
import React, { useMemo } from 'react';

import {
  buildDepExportComplianceReport,
  exportDepExportComplianceReportCsv,
  exportDepExportComplianceReportJson,
} from '@/features/rksv/dep-export-compliance/exportDepExportComplianceReport';
import {
  computeComplianceScore,
  type DepExportComplianceStatusDto,
  type DepExportRequirementDto,
} from '@/features/rksv/hooks/useDepExportCompliance';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

type Props = {
  status: DepExportComplianceStatusDto | undefined;
  requirements: DepExportRequirementDto[] | undefined;
  loading?: boolean;
  tenantSlug?: string | null;
  tenantName?: string | null;
};

function scoreStroke(score: number): string {
  if (score > 80) return '#52c41a';
  if (score > 50) return '#faad14';
  return '#cf1322';
}

export function DepExportComplianceReportCard({
  status,
  requirements,
  loading,
  tenantSlug,
  tenantName,
}: Props) {
  const { t } = useI18n();
  const notify = useNotify();
  const score = computeComplianceScore(status);

  const reportReady = Boolean(status);

  const metricCards = useMemo(
    () => [
      {
        key: 'completed',
        label: t('rksvHub.depExportCompliancePage.reportFulfilled'),
        value: `${status?.completedCount ?? 0}/${status?.totalRequirements ?? 0}`,
        background: 'rgba(82, 196, 26, 0.08)',
        color: '#389e0d',
      },
      {
        key: 'pending',
        label: t('rksvHub.depExportCompliancePage.reportPending'),
        value: String(status?.pendingCount ?? 0),
        background: 'rgba(250, 173, 20, 0.12)',
        color: '#d48806',
      },
      {
        key: 'overdue',
        label: t('rksvHub.depExportCompliancePage.reportOverdue'),
        value: String(status?.overdueCount ?? 0),
        background: 'rgba(207, 19, 34, 0.08)',
        color: '#cf1322',
      },
    ],
    [status, t]
  );

  const generateFullReport = (format: 'json' | 'csv') => {
    if (!status) {
      notify.warning('rksvHub.depExportCompliancePage.reportNotReady');
      return;
    }

    try {
      const report = buildDepExportComplianceReport({
        status,
        requirements: requirements ?? [],
        tenantSlug,
        tenantName,
      });
      if (format === 'csv') {
        exportDepExportComplianceReportCsv(report);
      } else {
        exportDepExportComplianceReportJson(report);
      }
      notify.successKey('rksvHub.depExportCompliancePage.reportGenerated');
    } catch (err) {
      notify.apiError(err, {
        logContext: 'DepExportCompliance.generateReport',
        fallbackKey: 'rksvHub.depExportCompliancePage.reportFailed',
      });
    }
  };

  return (
    <Card
      title={t('rksvHub.depExportCompliancePage.reportTitle')}
      loading={loading}
      style={{ marginBottom: 16 }}
      extra={
        <Space wrap>
          <Button
            icon={<FileTextOutlined />}
            disabled={!reportReady}
            onClick={() => generateFullReport('csv')}
          >
            {t('rksvHub.depExportCompliancePage.reportCsv')}
          </Button>
          <Button
            type="primary"
            icon={<DownloadOutlined />}
            disabled={!reportReady}
            onClick={() => generateFullReport('json')}
          >
            {t('rksvHub.depExportCompliancePage.reportGenerate')}
          </Button>
        </Space>
      }
    >
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginBottom: 16,
          gap: 12,
          flexWrap: 'wrap',
        }}
      >
        <Space>
          <Badge status={status?.isCompliant ? 'success' : 'error'} />
          <Typography.Text strong>
            {status?.isCompliant
              ? t('rksvHub.depExportCompliancePage.reportAllMet')
              : t('rksvHub.depExportCompliancePage.reportOutstanding')}
          </Typography.Text>
        </Space>
      </div>

      <Row gutter={[16, 16]}>
        {metricCards.map((metric) => (
          <Col xs={24} sm={8} key={metric.key}>
            <div
              style={{
                padding: 16,
                borderRadius: 8,
                background: metric.background,
              }}
            >
              <Typography.Text type="secondary">{metric.label}</Typography.Text>
              <div
                style={{
                  marginTop: 4,
                  fontSize: 28,
                  fontWeight: 700,
                  lineHeight: 1.2,
                  color: metric.color,
                }}
              >
                {metric.value}
              </div>
            </div>
          </Col>
        ))}
      </Row>

      <div style={{ marginTop: 20 }}>
        <Progress
          percent={score}
          strokeColor={scoreStroke(score)}
          format={(percent) =>
            t('rksvHub.depExportCompliancePage.reportScoreFormat', { percent: percent ?? 0 })
          }
        />
      </div>
    </Card>
  );
}
