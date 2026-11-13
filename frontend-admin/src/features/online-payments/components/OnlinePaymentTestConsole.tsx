'use client';

import { Alert, Button, Card, Form, Select, Space, Typography } from 'antd';
import { useState } from 'react';

import {
  type AdminOnlinePaymentDto,
  useOnlinePaymentTestMutation,
} from '@/features/online-payments/api/onlinePaymentsApi';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

const PRESET_AMOUNTS = [1, 5, 10, 12.34, 50];
const METHODS = ['card', 'paypal'] as const;

type ConsoleForm = {
  amount: number;
  paymentMethod: string;
};

type OnlinePaymentTestConsoleProps = {
  onPaymentCreated?: (payment: AdminOnlinePaymentDto) => void;
};

export function OnlinePaymentTestConsole({ onPaymentCreated }: OnlinePaymentTestConsoleProps) {
  const { t } = useI18n();
  const ts = (key: string, values?: Record<string, string | number>) =>
    t(`onlinePayments.${key}`, values);
  const notify = useNotify();
  const { modal } = useAntdApp();
  const [form] = Form.useForm<ConsoleForm>();
  const [lastPayment, setLastPayment] = useState<AdminOnlinePaymentDto | null>(null);
  const testMutation = useOnlinePaymentTestMutation();

  const runCreate = async (values: ConsoleForm) => {
    try {
      const result = await testMutation.mutateAsync({
        action: 'create',
        amount: values.amount,
        paymentMethod: values.paymentMethod,
      });
      if (!result.succeeded || !result.transaction) {
        notify.error(result.error || ts('loadError'));
        return;
      }
      setLastPayment(result.transaction);
      onPaymentCreated?.(result.transaction);
      notify.successKey('onlinePayments.console.createSuccess');
    } catch (err) {
      notify.apiError(err, {
        logContext: 'OnlinePayments.createTest',
        fallbackKey: 'common.errorGeneric',
      });
    }
  };

  const runWebhook = (kind: 'success' | 'failure' | 'expire') => {
    if (!lastPayment) {
      notify.warning(ts('console.noPaymentYet'));
      return;
    }

    const titleKey =
      kind === 'success'
        ? 'console.confirmSuccessTitle'
        : kind === 'expire'
          ? 'console.confirmExpireTitle'
          : 'console.confirmFailureTitle';
    const bodyKey =
      kind === 'success'
        ? 'console.confirmSuccessBody'
        : kind === 'expire'
          ? 'console.confirmExpireBody'
          : 'console.confirmFailureBody';
    const action =
      kind === 'success' ? 'webhookSucceeded' : kind === 'expire' ? 'expire' : 'webhookFailed';
    const toastKey =
      kind === 'success'
        ? 'onlinePayments.console.webhookSuccessToast'
        : kind === 'expire'
          ? 'onlinePayments.console.webhookExpireToast'
          : 'onlinePayments.console.webhookFailureToast';

    modal.confirm({
      title: ts(titleKey),
      content: ts(bodyKey),
      onOk: async () => {
        try {
          const result = await testMutation.mutateAsync({
            action,
            transactionId: lastPayment.id,
          });
          if (!result.succeeded || !result.transaction) {
            notify.error(result.error || ts('loadError'));
            return;
          }
          setLastPayment(result.transaction);
          notify.successKey(toastKey);
        } catch (err) {
          notify.apiError(err, {
            logContext: 'OnlinePayments.simulateWebhook',
            fallbackKey: 'common.errorGeneric',
          });
        }
      },
    });
  };

  return (
    <Card title={ts('console.title')}>
      <Alert type="info" showIcon style={{ marginBottom: 16 }} title={ts('console.intro')} />
      <Form<ConsoleForm>
        form={form}
        layout="vertical"
        initialValues={{ amount: 10, paymentMethod: 'card' }}
        onFinish={(values) => void runCreate(values)}
      >
        <Form.Item name="amount" label={ts('console.amount')} rules={[{ required: true }]}>
          <Select
            options={PRESET_AMOUNTS.map((value) => ({
              value,
              label: `${value.toFixed(2)} EUR`,
            }))}
          />
        </Form.Item>
        <Form.Item name="paymentMethod" label={ts('console.method')} rules={[{ required: true }]}>
          <Select
            options={METHODS.map((value) => ({
              value,
              label: ts(`console.methods.${value}`),
            }))}
          />
        </Form.Item>
        <Space wrap>
          <Button type="primary" htmlType="submit" loading={testMutation.isPending}>
            {ts('console.sendPayment')}
          </Button>
          <Button
            onClick={() => runWebhook('success')}
            disabled={!lastPayment}
            loading={testMutation.isPending}
          >
            {ts('console.webhookSuccess')}
          </Button>
          <Button
            danger
            onClick={() => runWebhook('failure')}
            disabled={!lastPayment}
            loading={testMutation.isPending}
          >
            {ts('console.webhookFailure')}
          </Button>
          <Button
            onClick={() => runWebhook('expire')}
            disabled={!lastPayment}
            loading={testMutation.isPending}
          >
            {ts('console.webhookExpire')}
          </Button>
        </Space>
      </Form>
      {lastPayment ? (
        <Typography.Paragraph type="secondary" style={{ marginTop: 16, marginBottom: 0 }}>
          {ts('console.lastPayment', { id: lastPayment.id })}
        </Typography.Paragraph>
      ) : null}
    </Card>
  );
}
