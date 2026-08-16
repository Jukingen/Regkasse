'use client';

import { Button, Card, Form, Input, Typography } from 'antd';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { activateUnifiedLicense } from '@/features/license/api/activateUnifiedLicense';
import { applyActivatedLicenseToCache } from '@/features/license/utils/applyActivatedLicenseToCache';
import { invalidateTenantLicenseQueries } from '@/features/license/utils/invalidateTenantLicenseQueries';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

type ActivateFormValues = {
  licenseKey: string;
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
  const [form] = Form.useForm<ActivateFormValues>();

  const mutation = useMutation({
    mutationFn: (values: ActivateFormValues) => activateUnifiedLicense(values.licenseKey),
    onSuccess: async (result) => {
      applyActivatedLicenseToCache(queryClient, {
        tenantId,
        validUntilUtc: result.validUntilUtc,
        licenseKey: result.licenseKey,
        licenseType: result.status,
      });
      if (tenantId) {
        await invalidateTenantLicenseQueries(queryClient, tenantId);
      }
      await queryClient.invalidateQueries({ queryKey: ['admin', 'license'], refetchType: 'all' });
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
        <Button type="primary" htmlType="submit" loading={mutation.isPending}>
          {t('license.management.activateButton')}
        </Button>
      </Form>
    </Card>
  );
}
