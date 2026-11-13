'use client';

import { CreditCardOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Checkbox, Descriptions, Form, Input, Space, Tag } from 'antd';
import Link from 'next/link';
import { useEffect } from 'react';

import {
  type PaymentGatewaySettings,
  buildWebhookUrl,
} from '@/features/settings/api/paymentGatewaySettingsApi';
import {
  usePaymentGatewaySettings,
  useUpdatePaymentGatewaySettings,
} from '@/features/settings/hooks/usePaymentGatewaySettings';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

type FormValues = {
  onlinePaymentMethods: string[];
};

const METHOD_OPTIONS = ['card', 'paypal', 'bank', 'cash', 'online'] as const;

export function PaymentGatewaySettingsForm() {
  const { message } = useAntdApp();
  const { t } = useI18n();
  const [form] = Form.useForm<FormValues>();
  const { data, isLoading, refetch } = usePaymentGatewaySettings();
  const updateSettings = useUpdatePaymentGatewaySettings();

  useEffect(() => {
    if (data) {
      form.setFieldsValue({
        onlinePaymentMethods: data.onlinePaymentMethods,
      });
    }
  }, [data, form]);

  const handleSubmit = async (values: FormValues) => {
    try {
      await updateSettings.mutateAsync({
        onlinePaymentMethods: values.onlinePaymentMethods ?? [],
      });
      message.success(t('settings.paymentGateway.saveSuccess'));
      void refetch();
    } catch {
      message.error(t('settings.paymentGateway.saveFailed'));
    }
  };

  const webhookUrl = data ? buildWebhookUrl(data.webhookPath) : '';

  return (
    <Card
      title={
        <span>
          <CreditCardOutlined style={{ marginRight: 8 }} />
          {t('settings.paymentGateway.cardTitle')}
        </span>
      }
      loading={isLoading}
    >
      <Alert
        title={t('settings.paymentGateway.secretsAlertTitle')}
        description={t('settings.paymentGateway.secretsAlertDescription')}
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
      />

      {data ? <GatewayStatusBlock data={data} webhookUrl={webhookUrl} /> : null}

      <Form form={form} layout="vertical" onFinish={handleSubmit} style={{ marginTop: 24 }}>
        <Form.Item label={t('settings.paymentGateway.stripeKeyLabel')}>
          <Input.Password
            value={data?.apiKeyConfigured ? '••••••••••••••••' : ''}
            disabled
            placeholder={t('settings.paymentGateway.stripeKeyPlaceholder')}
            visibilityToggle={false}
          />
        </Form.Item>

        <Form.Item label={t('settings.paymentGateway.webhookUrlLabel')}>
          <Input value={webhookUrl} disabled />
        </Form.Item>

        <Form.Item
          name="onlinePaymentMethods"
          label={t('settings.paymentGateway.paymentMethodsLabel')}
          extra={t('settings.paymentGateway.paymentMethodsExtra')}
        >
          <Checkbox.Group
            options={METHOD_OPTIONS.map((value) => ({
              value,
              label: t(`settings.paymentGateway.methods.${value}`),
            }))}
          />
        </Form.Item>

        <Space wrap>
          <Button type="primary" htmlType="submit" loading={updateSettings.isPending}>
            {t('settings.paymentGateway.save')}
          </Button>
          <Link href="/settings/payment-methods">
            {t('settings.paymentGateway.linkPaymentMethods')}
          </Link>
        </Space>
      </Form>
    </Card>
  );
}

function GatewayStatusBlock({
  data,
  webhookUrl,
}: {
  data: PaymentGatewaySettings;
  webhookUrl: string;
}) {
  const { t } = useI18n();
  return (
    <Descriptions bordered size="small" column={1} title={t('settings.paymentGateway.statusTitle')}>
      <Descriptions.Item label={t('settings.paymentGateway.providerLabel')}>
        <Tag color={data.isStripeProvider ? 'blue' : 'default'}>{data.provider}</Tag>
      </Descriptions.Item>
      <Descriptions.Item label={t('settings.paymentGateway.apiKeyStatus')}>
        <Tag color={data.apiKeyConfigured ? 'success' : 'warning'}>
          {data.apiKeyConfigured
            ? t('settings.paymentGateway.configured')
            : t('settings.paymentGateway.notConfigured')}
        </Tag>
      </Descriptions.Item>
      <Descriptions.Item label={t('settings.paymentGateway.webhookSecretStatus')}>
        <Tag color={data.webhookSecretConfigured ? 'success' : 'warning'}>
          {data.webhookSecretConfigured
            ? t('settings.paymentGateway.configured')
            : t('settings.paymentGateway.notConfigured')}
        </Tag>
      </Descriptions.Item>
      <Descriptions.Item label={t('settings.paymentGateway.webhookUrlLabel')}>
        <code style={{ fontSize: 12 }}>{webhookUrl}</code>
      </Descriptions.Item>
    </Descriptions>
  );
}
