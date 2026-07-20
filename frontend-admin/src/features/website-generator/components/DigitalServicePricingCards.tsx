'use client';

import { useQuery } from '@tanstack/react-query';
import { Card, Col, List, Row, Tag, Typography } from 'antd';
import { fetchDigitalServicePricing, type ServicePricing } from '@/features/website-generator/api/websiteGeneratorApi';
import { useI18n } from '@/i18n';

const { Text } = Typography;

function formatMoney(amount: number, currency: string, locale: string): string {
  try {
    return new Intl.NumberFormat(locale, { style: 'currency', currency }).format(amount);
  } catch {
    return `${amount.toFixed(2)} ${currency}`;
  }
}

type DigitalServicePricingCardsProps = {
  type?: 'website' | 'app';
};

export function DigitalServicePricingCards({ type }: DigitalServicePricingCardsProps) {
  const { t, formatLocale } = useI18n();

  const pricingQuery = useQuery({
    queryKey: ['admin', 'website', 'pricing', type ?? 'all'],
    queryFn: () => fetchDigitalServicePricing(type),
  });

  const items = pricingQuery.data ?? [];

  return (
    <Card
      title={t('tenants.digitalServices.pricingTitle')}
      loading={pricingQuery.isLoading}
      style={{ width: '100%' }}
    >
      <Row gutter={[16, 16]}>
        {items.map((plan: ServicePricing) => (
          <Col xs={24} md={12} xl={6} key={plan.serviceId}>
            <Card size="small" title={plan.name}>
              <Tag color={plan.type === 'website' ? 'blue' : 'purple'}>{plan.tier}</Tag>
              <div style={{ marginTop: 12 }}>
                <Text strong>
                  {formatMoney(plan.priceMonthly, plan.currency, formatLocale)}
                  <Text type="secondary"> / {t('tenants.digitalServices.perMonth')}</Text>
                </Text>
              </div>
              <div>
                <Text type="secondary">
                  {formatMoney(plan.priceYearly, plan.currency, formatLocale)}{' '}
                  {t('tenants.digitalServices.perYear')}
                </Text>
              </div>
              <List
                size="small"
                style={{ marginTop: 12 }}
                dataSource={plan.features}
                renderItem={(feature) => <List.Item style={{ padding: '4px 0' }}>{feature}</List.Item>}
              />
            </Card>
          </Col>
        ))}
      </Row>
    </Card>
  );
}
