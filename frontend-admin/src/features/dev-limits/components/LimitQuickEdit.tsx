'use client';

import { Button, Card, Form, InputNumber, Select, Space } from 'antd';
import { useEffect } from 'react';

import {
  DEV_LIMIT_FIELD_META,
  DEV_LIMIT_KEYS,
  type DevLimitKey,
  readDevLimitUsage,
} from '@/features/dev-limits/constants/limitKeys';
import type { TenantLimitUsageDto } from '@/features/tenants/api/tenantLimits';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

type LimitQuickEditValues = {
  limitKey: DevLimitKey;
  value: number;
};

type LimitQuickEditProps = {
  usage: TenantLimitUsageDto | undefined;
  disabled?: boolean;
  saving?: boolean;
  resetting?: boolean;
  onApply: (limitKey: DevLimitKey, value: number) => Promise<void>;
  onResetAll: () => Promise<void>;
};

export function LimitQuickEdit({
  usage,
  disabled,
  saving,
  resetting,
  onApply,
  onResetAll,
}: LimitQuickEditProps) {
  const { t } = useI18n();
  const { modal } = useAntdApp();
  const [form] = Form.useForm<LimitQuickEditValues>();
  const limitKey = Form.useWatch('limitKey', form) as DevLimitKey | undefined;

  useEffect(() => {
    if (!usage || !limitKey) return;
    const pair = readDevLimitUsage(limitKey, usage);
    form.setFieldValue('value', pair.limit);
  }, [form, limitKey, usage]);

  const meta = limitKey ? DEV_LIMIT_FIELD_META[limitKey] : undefined;

  const confirmReset = () => {
    modal.confirm({
      title: t('tenants.limits.devPanel.resetAll'),
      content: t('tenants.limits.devPanel.resetConfirm'),
      okText: t('tenants.limits.devPanel.resetAll'),
      okButtonProps: { danger: true },
      onOk: () => onResetAll(),
    });
  };

  return (
    <Card title={t('tenants.limits.devPanel.quickEditTitle')}>
      <Form
        form={form}
        layout="vertical"
        initialValues={{ limitKey: 'maxProductsPerTenant' }}
        onFinish={(values) => onApply(values.limitKey, values.value)}
        disabled={disabled}
      >
        <Form.Item
          name="limitKey"
          label={t('tenants.limits.devPanel.limit')}
          rules={[{ required: true }]}
        >
          <Select
            options={DEV_LIMIT_KEYS.map((key) => ({
              value: key,
              label: t(DEV_LIMIT_FIELD_META[key].labelKey),
            }))}
          />
        </Form.Item>
        <Form.Item
          name="value"
          label={t('tenants.limits.devPanel.value')}
          rules={[{ required: true, message: t('tenants.limits.required') }]}
        >
          <InputNumber
            min={meta?.money ? 0.01 : 1}
            max={meta?.money ? 1_000_000_000 : 1_000_000}
            precision={meta?.money ? 2 : 0}
            style={{ width: '100%', maxWidth: 320 }}
          />
        </Form.Item>
        <Space wrap>
          <Button type="primary" htmlType="submit" loading={saving}>
            {t('tenants.limits.devPanel.apply')}
          </Button>
          <Button danger onClick={confirmReset} loading={resetting}>
            {t('tenants.limits.devPanel.resetAll')}
          </Button>
        </Space>
      </Form>
    </Card>
  );
}
