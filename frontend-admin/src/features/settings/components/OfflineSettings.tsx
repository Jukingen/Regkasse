'use client';

import { DisconnectOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Form, InputNumber, Switch } from 'antd';
import { useEffect } from 'react';

import {
  DEFAULT_OFFLINE_SETTINGS,
  type OfflineSettings as OfflineSettingsValues,
} from '@/features/settings/api/offlineSettingsApi';
import {
  useOfflineSettings,
  useUpdateOfflineSettings,
} from '@/features/settings/hooks/useOfflineSettings';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

export function OfflineSettings() {
  const { message } = useAntdApp();
  const { t } = useI18n();
  const [form] = Form.useForm<OfflineSettingsValues>();
  const { data: settings, isLoading, refetch } = useOfflineSettings();
  const updateSettings = useUpdateOfflineSettings();

  useEffect(() => {
    if (settings) {
      form.setFieldsValue(settings);
    }
  }, [settings, form]);

  const handleSubmit = async (values: OfflineSettingsValues) => {
    try {
      await updateSettings.mutateAsync(values);
      message.success(t('settings.offline.saveSuccess'));
      void refetch();
    } catch {
      message.error(t('settings.offline.saveFailed'));
    }
  };

  return (
    <Card
      title={
        <span>
          <DisconnectOutlined style={{ marginRight: 8 }} />
          {t('settings.offline.cardTitle')}
        </span>
      }
      loading={isLoading}
    >
      <Alert
        title={t('settings.offline.alertTitle')}
        description={t('settings.offline.alertDescription')}
        type="warning"
        showIcon
        style={{ marginBottom: 16 }}
      />

      <Form
        form={form}
        layout="vertical"
        initialValues={DEFAULT_OFFLINE_SETTINGS}
        onFinish={handleSubmit}
      >
        <Form.Item
          name="maxOfflineTransactions"
          label={t('settings.offline.maxOfflineTransactionsLabel')}
          rules={[
            { required: true, message: t('settings.offline.maxOfflineTransactionsRequired') },
            { type: 'number', min: 1, max: 100, message: t('settings.offline.maxOfflineTransactionsRange') },
          ]}
        >
          <InputNumber min={1} max={100} style={{ width: 160 }} />
        </Form.Item>

        <Form.Item
          name="maxOfflineOrders"
          label={t('settings.offline.maxOfflineOrdersLabel')}
          rules={[
            { required: true, message: t('settings.offline.maxOfflineOrdersRequired') },
            { type: 'number', min: 1, max: 200, message: t('settings.offline.maxOfflineOrdersRange') },
          ]}
        >
          <InputNumber min={1} max={200} style={{ width: 160 }} />
        </Form.Item>

        <Form.Item
          name="offlineExpiryHours"
          label={t('settings.offline.offlineExpiryHoursLabel')}
          rules={[
            { required: true, message: t('settings.offline.offlineExpiryHoursRequired') },
            { type: 'number', min: 1, max: 168, message: t('settings.offline.offlineExpiryHoursRange') },
          ]}
          extra={t('settings.offline.offlineExpiryHoursHint')}
        >
          <InputNumber
            min={1}
            max={168}
            style={{ width: 160 }}
            addonAfter={t('settings.offline.hoursUnit')}
          />
        </Form.Item>

        <Form.Item
          name="tokenExpiryHours"
          label={t('settings.offline.tokenExpiryHoursLabel')}
          rules={[
            { required: true, message: t('settings.offline.tokenExpiryHoursRequired') },
            { type: 'number', min: 1, max: 720, message: t('settings.offline.tokenExpiryHoursRange') },
          ]}
        >
          <InputNumber
            min={1}
            max={720}
            style={{ width: 160 }}
            addonAfter={t('settings.offline.hoursUnit')}
          />
        </Form.Item>

        <Form.Item
          name="enableOfflineOrders"
          label={t('settings.offline.enableOfflineOrdersLabel')}
          valuePropName="checked"
        >
          <Switch
            checkedChildren={t('settings.offline.switchOn')}
            unCheckedChildren={t('settings.offline.switchOff')}
          />
        </Form.Item>

        <Form.Item
          name="enableOfflinePayments"
          label={t('settings.offline.enableOfflinePaymentsLabel')}
          valuePropName="checked"
          extra={t('settings.offline.enableOfflinePaymentsHint')}
        >
          <Switch
            checkedChildren={t('settings.offline.switchOn')}
            unCheckedChildren={t('settings.offline.switchOff')}
          />
        </Form.Item>

        <Form.Item>
          <Button type="primary" htmlType="submit" loading={updateSettings.isPending}>
            {t('settings.offline.save')}
          </Button>
        </Form.Item>
      </Form>
    </Card>
  );
}
