'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Form, Input, Select, Typography } from 'antd';
import { useMemo } from 'react';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { activateUnifiedLicense } from '@/features/license/api/activateUnifiedLicense';
import { parseUnifiedLicenseSlug } from '@/features/license/utils/unifiedLicenseRows';
import { applyActivatedLicenseToCache } from '@/features/license/utils/applyActivatedLicenseToCache';
import { invalidateTenantLicenseQueries } from '@/features/license/utils/invalidateTenantLicenseQueries';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
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
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const currentTenant = useCurrentTenant();
  const [form] = Form.useForm<ActivateFormValues>();
  const licenseKey = Form.useWatch('licenseKey', form);
  const parsedSlug = parseUnifiedLicenseSlug(licenseKey ?? '');
  const isSystemKey = parsedSlug === 'system';
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
        })),
    [tenantsQuery.data]
  );

  const mutation = useMutation({
    mutationFn: (values: ActivateFormValues) => {
      const selectedTenantId = isSystemKey
        ? null
        : values.tenantId?.trim() || tenantId?.trim() || null;
      return activateUnifiedLicense(values.licenseKey, selectedTenantId);
    },
    onSuccess: async (result, values) => {
      const selectedTenantId = isSystemKey
        ? null
        : values.tenantId?.trim() || tenantId?.trim() || null;
      applyActivatedLicenseToCache(queryClient, {
        tenantId: selectedTenantId,
        validUntilUtc: result.validUntilUtc,
        licenseKey: result.licenseKey,
        licenseType: result.status,
      });
      await invalidateTenantLicenseQueries(queryClient, selectedTenantId);
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

  return (
    <Card variant="borderless" title={t('license.management.activateTitle')}>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        {t('license.unified.formatHint')}
      </Typography.Paragraph>
      <Form
        form={form}
        layout="vertical"
        initialValues={{ tenantId: tenantId ?? undefined }}
        onFinish={(values) => mutation.mutate(values)}
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
        <Button type="primary" htmlType="submit" loading={mutation.isPending}>
          {t('license.management.activateButton')}
        </Button>
      </Form>
    </Card>
  );
}
