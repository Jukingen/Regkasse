'use client';

import { Card, Col, Row, Space, Statistic, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';

import { type DigitalBillingSubscriptionRow } from '@/features/billing/api/digitalBillingApi';
import { BillingAccessGate } from '@/features/billing/components/BillingAccessGate';
import { useDigitalBilling } from '@/features/billing/hooks/useDigitalBilling';
import { useI18n } from '@/i18n';

const { Paragraph } = Typography;

function statusColor(status: string): string {
  switch (status.toLowerCase()) {
    case 'active':
      return 'success';
    case 'cancelled':
      return 'default';
    case 'past_due':
      return 'warning';
    default:
      return 'processing';
  }
}

const KNOWN_STATUSES = new Set(['active', 'cancelled', 'past_due']);

function statusLabel(
  t: (key: string, options?: Record<string, string | number>) => string,
  status: string
): string {
  const normalized = status.toLowerCase();
  if (KNOWN_STATUSES.has(normalized)) {
    return t(`tenants.digitalServices.status.${normalized}`);
  }
  return status;
}

export function DigitalBillingDashboard() {
  const { t, formatLocale } = useI18n();
  const { data: billing, isLoading, isError } = useDigitalBilling();

  const formatMoney = (value: number, currency = billing?.currency ?? 'EUR') => {
    try {
      return new Intl.NumberFormat(formatLocale, { style: 'currency', currency }).format(value);
    } catch {
      return `${value.toFixed(2)} ${currency}`;
    }
  };

  const columns: ColumnsType<DigitalBillingSubscriptionRow> = [
    {
      title: t('billing.digital.columns.tenant'),
      dataIndex: 'tenant',
      key: 'tenant',
    },
    {
      title: t('billing.digital.columns.service'),
      dataIndex: 'service',
      key: 'service',
    },
    {
      title: t('billing.digital.columns.price'),
      dataIndex: 'price',
      key: 'price',
      render: (price: number, row) => formatMoney(price, row.currency),
    },
    {
      title: t('billing.digital.columns.startDate'),
      dataIndex: 'startDate',
      key: 'startDate',
      render: (value: string) => (value ? new Date(value).toLocaleDateString(formatLocale) : '—'),
    },
    {
      title: t('billing.digital.columns.nextBilling'),
      dataIndex: 'nextBilling',
      key: 'nextBilling',
      render: (value: string) => (value ? new Date(value).toLocaleDateString(formatLocale) : '—'),
    },
    {
      title: t('billing.digital.columns.status'),
      dataIndex: 'status',
      key: 'status',
      render: (status: string) => <Tag color={statusColor(status)}>{statusLabel(t, status)}</Tag>,
    },
  ];

  return (
    <BillingAccessGate>
      <Space orientation="vertical" size="large" style={{ width: '100%' }}>
        <Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('billing.digital.pageSubtitle')}
        </Paragraph>

        <Row gutter={[16, 16]}>
          <Col xs={24} sm={12} lg={6}>
            <Card variant="borderless" loading={isLoading}>
              <Statistic
                title={t('billing.digital.totalRevenue')}
                value={billing?.total ?? 0}
                precision={2}
                prefix="€"
              />
            </Card>
          </Col>
          <Col xs={24} sm={12} lg={6}>
            <Card variant="borderless" loading={isLoading}>
              <Statistic
                title={t('billing.digital.websites')}
                value={billing?.websites ?? 0}
                precision={2}
                prefix="€"
              />
            </Card>
          </Col>
          <Col xs={24} sm={12} lg={6}>
            <Card variant="borderless" loading={isLoading}>
              <Statistic
                title={t('billing.digital.apps')}
                value={billing?.apps ?? 0}
                precision={2}
                prefix="€"
              />
            </Card>
          </Col>
          <Col xs={24} sm={12} lg={6}>
            <Card variant="borderless" loading={isLoading}>
              <Statistic
                title={t('billing.digital.subscribers')}
                value={billing?.subscribers ?? 0}
              />
            </Card>
          </Col>
        </Row>

        <Card title={t('billing.digital.subscriptionsTitle')}>
          <Table<DigitalBillingSubscriptionRow>
            rowKey="id"
            loading={isLoading}
            dataSource={billing?.subscriptions ?? []}
            columns={columns}
            pagination={{ pageSize: 20 }}
            locale={{
              emptyText: isError ? t('common.errors.http500') : t('billing.digital.empty'),
            }}
          />
        </Card>
      </Space>
    </BillingAccessGate>
  );
}
