'use client';

import {
  AppstoreOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { Card, Col, Row, Statistic } from 'antd';

import type { DashboardSummaryDto } from '@/features/tenants/api/tenantLimits';
import { useI18n } from '@/i18n';

export function DashboardSummaryCards({ summary }: { summary?: DashboardSummaryDto }) {
  const { t } = useI18n();
  const total = summary?.total ?? 0;
  const healthy = summary?.healthy ?? 0;
  const warning = summary?.warning ?? 0;
  const critical = summary?.critical ?? 0;

  return (
    <Row gutter={[16, 16]}>
      <Col xs={24} sm={12} xl={6}>
        <Card variant="borderless">
          <Statistic
            title={t('tenants.limits.dashboard.summary.total')}
            value={total}
            prefix={<AppstoreOutlined />}
          />
        </Card>
      </Col>
      <Col xs={24} sm={12} xl={6}>
        <Card variant="borderless">
          <Statistic
            title={t('tenants.limits.dashboard.summary.healthy')}
            value={healthy}
            prefix={<CheckCircleOutlined />}
            styles={{ content: { color: '#389e0d' } }}
          />
        </Card>
      </Col>
      <Col xs={24} sm={12} xl={6}>
        <Card variant="borderless">
          <Statistic
            title={t('tenants.limits.dashboard.summary.warning')}
            value={warning}
            prefix={<WarningOutlined />}
            styles={{ content: { color: '#d48806' } }}
          />
        </Card>
      </Col>
      <Col xs={24} sm={12} xl={6}>
        <Card variant="borderless">
          <Statistic
            title={t('tenants.limits.dashboard.summary.critical')}
            value={critical}
            prefix={<CloseCircleOutlined />}
            styles={{ content: { color: '#cf1322' } }}
          />
        </Card>
      </Col>
    </Row>
  );
}
