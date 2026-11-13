'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Form, Input, Select, Space, Typography } from 'antd';
import { useMemo } from 'react';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { activateUnifiedLicense } from '@/features/license/api/activateUnifiedLicense';
import {
  isLicenseSlugMismatch,
  parseUnifiedLicenseSlug,
  resolveLicenseUnlockTarget,
} from '@/features/license/utils/unifiedLicenseRows';
import { applyActivatedLicenseToCache } from '@/features/license/utils/applyActivatedLicenseToCache';
import { invalidateTenantLicenseQueries } from '@/features/license/utils/invalidateTenantLicenseQueries';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

type ActivateFormValues = {
  licenseKey: string;
  tenantId?: string;
};

type UnifiedLicenseActivateCardProps = {
  tenantId?: string | null;
  onActivated?: () => void;
};

export function UnifiedLicenseActivateCard({
  tenantId,
  onActivated,
}: UnifiedLicenseActivateCardProps) {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const currentTenant = useCurrentTenant();
  const [form] = Form.useForm<ActivateFormValues>();
  const licenseKey = Form.useWatch('licenseKey', form);
  const selectedTenantId = Form.useWatch('tenantId', form);
  const parsedSlug = parseUnifiedLicenseSlug(licenseKey ?? '');
  const unlockTarget = resolveLicenseUnlockTarget(licenseKey ?? '');
  const isSystemKey = unlockTarget === 'system';
  const showTenantSelect =
    isSuperAdmin(user?.role) &&
    currentTenant.isSuperAdminPlatformMode &&
    !isSystemKey;

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', false],
    queryFn: () => listAdminTenants(false),
    enabled: showTenantSelect,
  });

  const tenantOptions = useMemo(
    () =>
      (tenantsQuery.data ?? [])
        .filter((row) => row.status === 'active')
        .map((row) => ({
          value: row.id,
          label: `${row.name} (${row.slug})`,
          slug: row.slug,
        })),
    [tenantsQuery.data]
  );

  const selectedTenantSlug =
    tenantOptions.find((row) => row.value === selectedTenantId)?.slug ?? currentTenant.tenantSlug;
  const slugMismatch = isLicenseSlugMismatch(licenseKey ?? '', selectedTenantSlug);

  const mutation = useMutation({
    mutationFn: (values: ActivateFormValues) => {
      const selectedId = isSystemKey
        ? null
        : values.tenantId?.trim() || tenantId?.trim() || null;
      return activateUnifiedLicense(values.licenseKey, selectedId);
    },
    onSuccess: async (result, values) => {
      const selectedId = isSystemKey
        ? null
        : values.tenantId?.trim() || tenantId?.trim() || null;
      applyActivatedLicenseToCache(queryClient, {
        tenantId: selectedId,
        validUntilUtc: result.validUntilUtc,
        licenseKey: result.licenseKey,
        licenseType: result.status,
      });
      await invalidateTenantLicenseQueries(queryClient, selectedId);
      await queryClient.invalidateQueries({ queryKey: ['admin', 'license'], refetchType: 'all' });
      await queryClient.invalidateQueries({ queryKey: ['billing'], refetchType: 'all' });
      notify.success(result.message?.trim() || t('license.management.activateSuccess'));
      form.resetFields();
      onActivated?.();
    },
    onError: (error: unknown) => {
      const axiosError = error as { response?: { data?: { message?: string } } };
      const msg = axiosError.response?.data?.message?.trim();
      notify.error(msg || t('license.management.activateError'));
    },
  });

  const submit = (values: ActivateFormValues) => {
    mutation.mutate(values);
  };

  const onFinish = (values: ActivateFormValues) => {
    if (isSystemKey) {
      modal.confirm({
        title: t('license.management.activateSystemConfirmTitle'),
        content: t('license.management.activateSystemConfirmContent'),
        okText: t('license.management.activateButton'),
        cancelText: t('common.buttons.cancel'),
        onOk: () => submit(values),
      });
      return;
    }
    if (slugMismatch) {
      modal.confirm({
        title: t('license.management.activateSlugMismatchTitle'),
        content: t('license.management.activateSlugMismatchContent', {
          slug: parsedSlug ?? '',
          current: selectedTenantSlug ?? '',
        }),
        okText: t('license.management.activateButton'),
        cancelText: t('common.buttons.cancel'),
        onOk: () => submit(values),
      });
      return;
    }
    submit(values);
  };

  return (
    <Card variant="borderless" title={t('license.management.activateTitle')}>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        {t('license.unified.formatHint')}
      </Typography.Paragraph>
      <Alert
        type="info"
        showIcon
        title={t('license.management.systemDoesNotUnlockTenant')}
        style={{ marginBottom: 16 }}
      />
      <Form
        form={form}
        layout="vertical"
        initialValues={{ tenantId: tenantId ?? undefined }}
        onFinish={onFinish}
      >
        <Form.Item
          name="licenseKey"
          label={t('license.mandant.licenseKey')}
          rules={[{ required: true, message: t('license.extendModal.noLicenseKey') }]}
        >
          <Input
            placeholder={t('license.unified.keyPlaceholder')}
            autoComplete="off"
            disabled={mutation.isPending}
          />
        </Form.Item>
        {unlockTarget === 'system' ? (
          <Alert
            type="warning"
            showIcon
            title={t('license.management.activateUnlocksSystem')}
            description={t('license.management.activateSystemConfirmContent')}
            style={{ marginBottom: 16 }}
          />
        ) : null}
        {unlockTarget === 'tenant' ? (
          <Alert
            type="success"
            showIcon
            title={t('license.management.activateUnlocksTenant')}
            style={{ marginBottom: 16 }}
          />
        ) : null}
        {slugMismatch ? (
          <Alert
            type="warning"
            showIcon
            title={t('license.management.slugMismatchWarning', {
              slug: parsedSlug ?? '',
              current: selectedTenantSlug ?? '',
            })}
            style={{ marginBottom: 16 }}
          />
        ) : null}
        {showTenantSelect ? (
          <Form.Item
            name="tenantId"
            label={t('license.management.activateTenantLabel')}
            extra={t('license.management.activateTenantHint')}
          >
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              placeholder={t('license.management.activateTenantPlaceholder')}
              loading={tenantsQuery.isLoading}
              options={tenantOptions}
              disabled={mutation.isPending}
            />
          </Form.Item>
        ) : null}
        <Space>
          <Button type="primary" htmlType="submit" loading={mutation.isPending}>
            {t('license.management.activateButton')}
          </Button>
        </Space>
      </Form>
    </Card>
  );
}
