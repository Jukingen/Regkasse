'use client';

import { Alert, Button, Card, Form, InputNumber, Space } from 'antd';
import { useEffect } from 'react';

import {
  type TenantLimitsDto,
  type UpdateTenantLimitsRequest,
} from '@/features/tenants/api/tenantLimits';
import {
  useResetTenantLimits,
  useTenantLimits,
  useUpdateTenantLimits,
} from '@/features/tenants/hooks/useTenantLimits';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

const DEFAULTS: UpdateTenantLimitsRequest = {
  maxActiveRegistersPerUser: 5,
  maxProductsPerTenant: 10000,
  maxUsersPerTenant: 50,
  dailyMaxTransactions: 1000,
  maxTransactionAmount: 10000,
  dailyMaxRevenue: 50000,
  maxBackupsPerTenant: 50,
  maxBackupSizeMB: 500,
  maxOfflineTransactions: 50,
};

const LIMIT_FIELDS = [
  {
    key: 'maxActiveRegistersPerUser',
    labelKey: 'tenants.limits.maxActiveRegistersPerUser',
    min: 1,
    max: 20,
  },
  { key: 'maxProductsPerTenant', labelKey: 'tenants.limits.maxProductsPerTenant', min: 100, max: 100000 },
  { key: 'maxUsersPerTenant', labelKey: 'tenants.limits.maxUsersPerTenant', min: 5, max: 500 },
  { key: 'dailyMaxTransactions', labelKey: 'tenants.limits.dailyMaxTransactions', min: 10, max: 10000 },
  {
    key: 'maxTransactionAmount',
    labelKey: 'tenants.limits.maxTransactionAmount',
    min: 100,
    max: 100000,
    money: true,
  },
  {
    key: 'dailyMaxRevenue',
    labelKey: 'tenants.limits.dailyMaxRevenue',
    min: 1000,
    max: 500000,
    money: true,
  },
  { key: 'maxBackupsPerTenant', labelKey: 'tenants.limits.maxBackupsPerTenant', min: 5, max: 200 },
  { key: 'maxBackupSizeMB', labelKey: 'tenants.limits.maxBackupSizeMB', min: 50, max: 5000 },
  { key: 'maxOfflineTransactions', labelKey: 'tenants.limits.maxOfflineTransactions', min: 10, max: 500 },
] as const;

function toFormValues(data: TenantLimitsDto | undefined): UpdateTenantLimitsRequest {
  if (!data) return DEFAULTS;
  return {
    maxActiveRegistersPerUser: data.maxActiveRegistersPerUser,
    maxProductsPerTenant: data.maxProductsPerTenant,
    maxUsersPerTenant: data.maxUsersPerTenant,
    dailyMaxTransactions: data.dailyMaxTransactions,
    maxTransactionAmount: data.maxTransactionAmount,
    dailyMaxRevenue: data.dailyMaxRevenue,
    maxBackupsPerTenant: data.maxBackupsPerTenant,
    maxBackupSizeMB: data.maxBackupSizeMB,
    maxOfflineTransactions: data.maxOfflineTransactions,
  };
}

export function TenantLimitsSettings({ tenantId }: { tenantId: string }) {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const [form] = Form.useForm<UpdateTenantLimitsRequest>();
  const query = useTenantLimits(tenantId);
  const updateMutation = useUpdateTenantLimits(tenantId);
  const resetMutation = useResetTenantLimits(tenantId);

  useEffect(() => {
    form.setFieldsValue(toFormValues(query.data));
  }, [form, query.data]);

  const confirmReset = () => {
    modal.confirm({
      title: t('tenants.limits.reset'),
      content: t('tenants.limits.resetConfirm'),
      okText: t('tenants.limits.reset'),
      okButtonProps: { danger: true },
      onOk: () =>
        resetMutation.mutateAsync().then(
          (data) => {
            form.setFieldsValue(toFormValues(data));
            notify.successKey('tenants.limits.saved');
          },
          (err: unknown) => {
            notify.apiError(err, {
              logContext: 'TenantLimits.reset',
              fallbackKey: 'tenants.limits.error',
            });
          }
        ),
    });
  };

  return (
    <Card title={t('tenants.limits.title')} loading={query.isLoading && !query.data}>
      {query.isError ? (
        <Alert type="error" title={t('tenants.limits.error')} style={{ marginBottom: 16 }} />
      ) : (
        <Alert
          type="info"
          title={t('tenants.limits.overrideNote')}
          style={{ marginBottom: 16 }}
        />
      )}
      <Form
        form={form}
        layout="vertical"
        initialValues={DEFAULTS}
        onFinish={(values) => {
          updateMutation.mutate(values, {
            onSuccess: (data) => {
              form.setFieldsValue(toFormValues(data));
              notify.successKey('tenants.limits.saved');
            },
            onError: (err) => {
              notify.apiError(err, {
                logContext: 'TenantLimits.save',
                fallbackKey: 'tenants.limits.error',
              });
            },
          });
        }}
      >
        {LIMIT_FIELDS.map((field) => (
          <Form.Item
            key={field.key}
            label={t(field.labelKey)}
            name={field.key}
            rules={[{ required: true, message: t('tenants.limits.required') }]}
          >
            <InputNumber
              min={field.min}
              max={field.max}
              precision={'money' in field ? 2 : 0}
              style={{ width: '100%', maxWidth: 320 }}
            />
          </Form.Item>
        ))}
        <Space wrap>
          <Button type="primary" htmlType="submit" loading={updateMutation.isPending}>
            {t('tenants.limits.save')}
          </Button>
          <Button danger onClick={confirmReset} loading={resetMutation.isPending}>
            {t('tenants.limits.reset')}
          </Button>
        </Space>
      </Form>
    </Card>
  );
}
