'use client';

import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, DatePicker, Descriptions, Form, Select, Space, Tag } from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { useEffect, useMemo } from 'react';

import { CardSkeleton, FormSkeleton } from '@/components/Skeleton';
import { isDevelopment } from '@/features/auth/services/devTenant';
import {
  LICENSE_TEST_MOCK_SCENARIOS,
  licenseTestScenarioButtonColor,
  licenseTestScenarioFromDays,
  licenseTestScenarioLabelKey,
  licenseTestStatusTagColor,
} from '@/features/license/constants/licenseTestScenarios';
import { useLicenseTest } from '@/features/license/hooks/useLicenseTest';
import { useUpdateLicenseTest } from '@/features/license/hooks/useUpdateLicenseTest';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useTenant } from '@/features/tenancy/providers/TenantProvider';
import { useAntdApp } from '@/hooks/useAntdApp';
import { DAYJS_DATETIME_FORMAT, formatGermanDateTime, useI18n } from '@/i18n';
import { technicalConsole } from '@/shared/dev/technicalConsole';

type LicenseTestFormValues = {
  tenantId: string;
  validUntil: Dayjs;
};

export function LicenseTestPanel() {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const [form] = Form.useForm<LicenseTestFormValues>();
  const { updateMutation, scenarioMutation, isPending } = useUpdateLicenseTest();
  const { tenant: contextTenant, isLoading: tenantLoading, error: tenantError } = useTenant();
  const tenantId = Form.useWatch('tenantId', form);

  // Keep the panel aligned with header / TenantProvider mandant context.
  useEffect(() => {
    if (!contextTenant?.id) {
      return;
    }
    const current = form.getFieldValue('tenantId') as string | undefined;
    if (current !== contextTenant.id) {
      form.setFieldsValue({ tenantId: contextTenant.id });
    }
  }, [contextTenant?.id, form]);

  useEffect(() => {
    if (!isDevelopment()) {
      return;
    }
    technicalConsole.devLog('[License Test] tenant:', contextTenant);
    technicalConsole.devLog('[License Test] tenantId:', contextTenant?.id ?? null);
  }, [contextTenant]);

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', false],
    queryFn: () => listAdminTenants(false),
    enabled: isDevelopment(),
  });

  const snapshotQuery = useLicenseTest(tenantId);

  const tenantOptions = useMemo(
    () =>
      (tenantsQuery.data ?? [])
        .filter((row) => row.status === 'active')
        .map((row) => ({
          value: row.id,
          label: `${row.name} (${row.slug})`,
        })),
    [tenantsQuery.data]
  );

  const scenarios = LICENSE_TEST_MOCK_SCENARIOS;

  const requireTenantId = (): string | null => {
    const id = form.getFieldValue('tenantId') as string | undefined;
    if (!id) {
      message.warning(t('license.testPanel.noTenantSelected'));
      return null;
    }
    return id;
  };

  const handleScenario = async (days: number) => {
    const id = requireTenantId();
    if (!id) return;

    const scenario = licenseTestScenarioFromDays(days);
    if (!scenario) return;

    try {
      await scenarioMutation.mutateAsync({ tenantId: id, scenario });
    } catch {
      // Toast handled in useUpdateLicenseTest.onError
    }
  };

  const handleSubmit = async (values: LicenseTestFormValues) => {
    try {
      await updateMutation.mutateAsync({
        tenantId: values.tenantId,
        validUntil: values.validUntil.toISOString(),
      });
    } catch {
      // Toast handled in useUpdateLicenseTest.onError
    }
  };

  const tenant = snapshotQuery.data?.tenant;
  const deployment = snapshotQuery.data?.deployment;

  if (tenantLoading && !contextTenant) {
    return <FormSkeleton fields={3} loading />;
  }

  if (tenantError && !contextTenant) {
    return (
      <Alert
        type="error"
        showIcon
        message={t('license.testPanel.tenantLoadError')}
        description={tenantError.message}
      />
    );
  }

  return (
    <Form
      form={form}
      layout="vertical"
      initialValues={{
        tenantId: contextTenant?.id,
        validUntil: dayjs().add(30, 'day'),
      }}
      onFinish={handleSubmit}
    >
      {!contextTenant ? (
        <Alert
          type="warning"
          showIcon
          message={t('license.testPanel.noTenantSelected')}
          style={{ marginBottom: 16 }}
        />
      ) : null}

      <Card title={t('license.testPanel.selectTenant')} style={{ marginBottom: 16 }}>
        <Form.Item
          name="tenantId"
          label={t('license.testPanel.selectTenant')}
          rules={[{ required: true, message: t('license.testPanel.noTenantSelected') }]}
          extra={
            contextTenant
              ? t('license.testPanel.contextTenantHint', {
                  name: contextTenant.name,
                  slug: contextTenant.slug,
                })
              : undefined
          }
        >
          <Select
            showSearch
            optionFilterProp="label"
            placeholder={t('license.testPanel.selectTenantPlaceholder')}
            loading={tenantsQuery.isLoading}
            options={tenantOptions}
          />
        </Form.Item>
      </Card>

      {tenantId ? (
        <Card
          title={t('license.testPanel.tenantSection')}
          size="small"
          style={{ marginBottom: 16 }}
        >
          {snapshotQuery.isLoading ? (
            <CardSkeleton count={1} loading />
          ) : tenant ? (
            <Descriptions column={1} size="small">
              <Descriptions.Item label={t('license.testPanel.selectTenant')}>
                {`${tenant.name} (${tenant.slug})`}
              </Descriptions.Item>
              <Descriptions.Item label={t('license.testPanel.status')}>
                <Tag color={licenseTestStatusTagColor(tenant.status)}>
                  {t(
                    `license.testPanel.statusLabels.${tenant.status}` as 'license.testPanel.statusLabels.active'
                  )}
                </Tag>
              </Descriptions.Item>
              <Descriptions.Item label={t('license.testPanel.validUntil')}>
                {formatGermanDateTime(tenant.validUntilUtc)}
              </Descriptions.Item>
              <Descriptions.Item label={t('license.testPanel.daysRemaining')}>
                {!tenant.validUntilUtc && tenant.daysRemaining >= 999
                  ? t('license.testPanel.unlimitedNoExpiry')
                  : tenant.daysRemaining}
              </Descriptions.Item>
              <Descriptions.Item label={t('license.testPanel.licenseKey')}>
                {tenant.licenseKey ?? '—'}
              </Descriptions.Item>
            </Descriptions>
          ) : null}
        </Card>
      ) : null}

      {tenantId && deployment ? (
        <Card
          title={t('license.testPanel.deploymentSection')}
          size="small"
          style={{ marginBottom: 16 }}
        >
          <Descriptions column={1} size="small">
            <Descriptions.Item label={t('license.testPanel.daysRemaining')}>
              {deployment.daysRemaining}
            </Descriptions.Item>
            <Descriptions.Item label={t('license.testPanel.mode')}>
              {deployment.mode}
            </Descriptions.Item>
            <Descriptions.Item label={t('license.testPanel.validUntil')}>
              {formatGermanDateTime(deployment.expiryDateUtc)}
            </Descriptions.Item>
          </Descriptions>
        </Card>
      ) : null}

      <Card title={t('license.testPanel.simulationTitle')} style={{ marginBottom: 16 }}>
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          {scenarios.map((s) => (
            <Button
              key={s.status}
              block
              color={licenseTestScenarioButtonColor(s.color)}
              variant={s.status === 'expired' ? 'solid' : 'outlined'}
              onClick={() => void handleScenario(s.days)}
              loading={isPending}
            >
              {t(licenseTestScenarioLabelKey(s.status))}
            </Button>
          ))}
        </Space>
      </Card>

      <Card title={t('license.testPanel.manualTitle')}>
        <Form.Item
          name="validUntil"
          label={t('license.testPanel.validUntil')}
          rules={[{ required: true }]}
        >
          <DatePicker showTime format={DAYJS_DATETIME_FORMAT} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item>
          <Button type="primary" htmlType="submit" loading={isPending}>
            {t('license.testPanel.updateButton')}
          </Button>
        </Form.Item>
      </Card>
    </Form>
  );
}
