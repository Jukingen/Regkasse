'use client';

import { GlobalOutlined, SyncOutlined } from '@ant-design/icons';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Col, Drawer, Row, Space, Tag, Typography } from 'antd';
import { useState } from 'react';

import {
  type CustomerDigitalService,
  syncDigitalMenu,
} from '@/features/website-generator/api/customerPortalApi';
import { useCustomerServices } from '@/features/website-generator/hooks/useCustomerServices';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';

const { Paragraph, Text } = Typography;

function formatMoney(amount: number, currency: string, locale: string): string {
  try {
    return new Intl.NumberFormat(locale, { style: 'currency', currency }).format(amount);
  } catch {
    return `${amount.toFixed(2)} ${currency}`;
  }
}

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

type CustomerPortalPanelProps = {
  tenantId?: string;
};

export function CustomerPortalPanel({ tenantId }: CustomerPortalPanelProps) {
  const { t, formatLocale } = useI18n();
  const { message } = useAntdApp();
  const queryClient = useQueryClient();
  const { data: services, isLoading, isError } = useCustomerServices(tenantId);
  const [activeService, setActiveService] = useState<CustomerDigitalService | null>(null);

  const syncMutation = useMutation({
    mutationFn: () => syncDigitalMenu(tenantId),
    onSuccess: (result) => {
      if (!result.succeeded) {
        message.error(result.error ?? t('common.errors.http500'));
        return;
      }
      message.success(t('tenants.digitalServices.menuSyncSuccess'));
      void queryClient.invalidateQueries({ queryKey: ['admin', 'website', 'my-services'] });
    },
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, { logContext: 'CustomerPortalPanel' }),
  });

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {t('tenants.digitalServices.customerPortalSubtitle')}
      </Paragraph>

      {isError ? <Alert type="error" showIcon message={t('common.errors.http500')} /> : null}

      <Row gutter={[16, 16]}>
        {(services ?? []).map((service) => (
          <Col xs={24} md={12} xl={8} key={service.id}>
            <Card
              loading={isLoading}
              title={service.name}
              extra={
                <Tag color={statusColor(service.status)}>{statusLabel(t, service.status)}</Tag>
              }
            >
              <Space orientation="vertical" size="small" style={{ width: '100%' }}>
                <Text>
                  <Text type="secondary">{t('tenants.digitalServices.priceLabel')}: </Text>
                  {formatMoney(service.price, service.currency, formatLocale)}
                  {' / '}
                  {t('tenants.digitalServices.perMonth')}
                </Text>
                <Text>
                  <Text type="secondary">{t('tenants.digitalServices.statusLabel')}: </Text>
                  {statusLabel(t, service.status)}
                </Text>
                {service.url ? (
                  <Text>
                    <Text type="secondary">{t('tenants.digitalServices.urlLabel')}: </Text>
                    <a href={service.url} target="_blank" rel="noreferrer">
                      {service.url}
                    </a>
                  </Text>
                ) : (
                  <Text type="secondary">{t('tenants.digitalServices.urlMissing')}</Text>
                )}
                <Button
                  type="primary"
                  icon={<GlobalOutlined />}
                  onClick={() => setActiveService(service)}
                >
                  {t('tenants.digitalServices.manage')}
                </Button>
              </Space>
            </Card>
          </Col>
        ))}
      </Row>

      {!isLoading && (services?.length ?? 0) === 0 ? (
        <Alert
          type="info"
          showIcon
          message={t('tenants.digitalServices.noSubscriptions')}
          description={t('tenants.digitalServices.noSubscriptionsHint')}
        />
      ) : null}

      <Card title={t('tenants.digitalServices.menuManagementTitle')}>
        <Paragraph type="secondary">{t('tenants.digitalServices.menuManagementHint')}</Paragraph>
        <Button
          type="primary"
          icon={<SyncOutlined />}
          loading={syncMutation.isPending}
          onClick={() => syncMutation.mutate()}
        >
          {t('tenants.digitalServices.menuSync')}
        </Button>
        {syncMutation.data?.succeeded ? (
          <Space orientation="vertical" style={{ marginTop: 16, width: '100%' }}>
            {syncMutation.data.websiteUrl ? (
              <Text>
                {t('tenants.digitalServices.websiteCardTitle')}:{' '}
                <a href={syncMutation.data.websiteUrl} target="_blank" rel="noreferrer">
                  {syncMutation.data.websiteUrl}
                </a>
              </Text>
            ) : null}
            {syncMutation.data.appUrl ? (
              <Text>
                {t('tenants.digitalServices.appCardTitle')}:{' '}
                <a href={syncMutation.data.appUrl} target="_blank" rel="noreferrer">
                  {syncMutation.data.appUrl}
                </a>
              </Text>
            ) : null}
          </Space>
        ) : null}
      </Card>

      <Drawer
        title={activeService?.name}
        open={!!activeService}
        onClose={() => setActiveService(null)}
        width={420}
        destroyOnHidden
      >
        {activeService ? (
          <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
            <Text>
              {t('tenants.digitalServices.serviceId')}: {activeService.serviceId}
            </Text>
            <Text>
              {t('tenants.digitalServices.tierLabel')}: {activeService.tier}
            </Text>
            <Text>
              {t('tenants.digitalServices.nextBilling')}:{' '}
              {activeService.nextBillingDate
                ? new Date(activeService.nextBillingDate).toLocaleDateString(formatLocale)
                : '—'}
            </Text>
            {activeService.url ? (
              <Button
                type="primary"
                href={activeService.url}
                target="_blank"
                rel="noreferrer"
                icon={<GlobalOutlined />}
              >
                {t('tenants.digitalServices.openUrl')}
              </Button>
            ) : null}
            <Button icon={<SyncOutlined />} onClick={() => syncMutation.mutate()}>
              {t('tenants.digitalServices.menuSync')}
            </Button>
          </Space>
        ) : null}
      </Drawer>
    </Space>
  );
}
