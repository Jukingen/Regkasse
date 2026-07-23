'use client';

import {
  CheckCircleOutlined,
  InfoCircleOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { Alert, Button, Card, Col, List, Modal, Row, Space, Statistic, Typography } from 'antd';

import type { ImpactReport, ImpactSeverity } from '@/features/impact/types';
import { useI18n } from '@/i18n';
import { formatCurrency } from '@/i18n/formatting';

export type ImpactSimulatorProps = {
  open: boolean;
  impactReport: ImpactReport | null;
  confirmLoading?: boolean;
  onClose: () => void;
  onConfirm: () => void;
};

function alertTypeForSeverity(severity: ImpactSeverity): 'error' | 'warning' | 'info' {
  const normalized = String(severity).toLowerCase();
  if (normalized === 'critical' || normalized === 'high') return 'error';
  if (normalized === 'warning' || normalized === 'medium') return 'warning';
  return 'info';
}

function isHighRisk(severity: ImpactSeverity): boolean {
  const normalized = String(severity).toLowerCase();
  return normalized === 'critical' || normalized === 'high';
}

/**
 * Read-only preview modal for critical / sensitive setting changes.
 * Shows counts, recommendations, and warnings from the impact simulation API.
 */
export function ImpactSimulator({
  open,
  impactReport,
  confirmLoading = false,
  onClose,
  onConfirm,
}: ImpactSimulatorProps) {
  const { t, formatLocale } = useI18n();

  if (!impactReport) return null;

  const highRisk = isHighRisk(impactReport.severity);
  const moneyLabel =
    impactReport.estimatedFinancialImpact == null
      ? null
      : formatCurrency(impactReport.estimatedFinancialImpact, formatLocale, {
          currency: (impactReport.estimatedFinancialImpactCurrency || 'EUR').trim() || 'EUR',
        });
  const products = impactReport.affectedRecords?.products ?? 0;
  const payments = impactReport.affectedRecords?.payments ?? 0;
  const invoices = impactReport.affectedRecords?.invoices ?? 0;
  const recommendations = impactReport.recommendations ?? [];
  const warnings = impactReport.warnings ?? [];

  return (
    <Modal
      title={t('impactSimulator.modalTitle')}
      open={open}
      onCancel={onClose}
      width={800}
      destroyOnHidden
      maskClosable={!confirmLoading}
      keyboard={!confirmLoading}
      footer={[
        <Button key="cancel" onClick={onClose} disabled={confirmLoading}>
          {t('impactSimulator.cancel')}
        </Button>,
        <Button
          key="confirm"
          type="primary"
          danger={highRisk}
          loading={confirmLoading}
          onClick={onConfirm}
        >
          {t('impactSimulator.confirm')}
        </Button>,
      ]}
    >
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        <Alert
          type={alertTypeForSeverity(impactReport.severity)}
          showIcon
          title={impactReport.title}
          description={impactReport.summary}
        />

        <Row gutter={[16, 16]}>
          <Col xs={24} sm={12} md={6}>
            <Statistic
              title={t('impactSimulator.statProducts')}
              value={products}
              valueStyle={{ color: products > 0 ? '#d48806' : '#389e0d' }}
            />
          </Col>
          <Col xs={24} sm={12} md={6}>
            <Statistic title={t('impactSimulator.statPayments')} value={payments} />
          </Col>
          <Col xs={24} sm={12} md={6}>
            <Statistic title={t('impactSimulator.statInvoices')} value={invoices} />
          </Col>
          <Col xs={24} sm={12} md={6}>
            <Statistic
              title={t('impactSimulator.statFinancial')}
              value={moneyLabel ?? t('impactSimulator.noFinancialEstimate')}
              valueStyle={{
                color:
                  impactReport.estimatedFinancialImpact != null &&
                  impactReport.estimatedFinancialImpact !== 0
                    ? '#cf1322'
                    : undefined,
                fontSize: moneyLabel ? undefined : 14,
              }}
            />
          </Col>
        </Row>

        {recommendations.length > 0 ? (
          <Card title={t('impactSimulator.recommendations')} size="small">
            <List
              size="small"
              dataSource={recommendations}
              renderItem={(item) => (
                <List.Item>
                  <Space align="start">
                    <CheckCircleOutlined style={{ color: '#389e0d', marginTop: 4 }} />
                    <Typography.Text>{item}</Typography.Text>
                  </Space>
                </List.Item>
              )}
            />
          </Card>
        ) : null}

        {warnings.length > 0 ? (
          <Card title={t('impactSimulator.warnings')} size="small">
            <List
              size="small"
              dataSource={warnings}
              renderItem={(item) => (
                <List.Item>
                  <Space align="start">
                    <WarningOutlined style={{ color: '#cf1322', marginTop: 4 }} />
                    <Typography.Text>{item}</Typography.Text>
                  </Space>
                </List.Item>
              )}
            />
          </Card>
        ) : null}

        <Alert
          type="info"
          showIcon
          icon={<InfoCircleOutlined />}
          title={t('impactSimulator.historicalTitle')}
          description={t('impactSimulator.historicalBody')}
        />
      </Space>
    </Modal>
  );
}
