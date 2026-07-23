'use client';

import { useMutation } from '@tanstack/react-query';
import { Card, Form, Input, Select, Space } from 'antd';

import { SaveButton } from '@/components/SaveButton';
import { type AdminTenantDetail, updateAdminTenant } from '@/features/super-admin/api/adminTenants';
import { TenantDetailDangerZone } from '@/features/super-admin/components/TenantDetailDangerZone';
import { TenantOperationModeCard } from '@/features/super-admin/components/TenantOperationModeCard';
import { useCanManageTenantDeletion } from '@/features/super-admin/hooks/useCanManageTenantDeletion';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

export type TenantDetailSettingsTabProps = {
  tenant: AdminTenantDetail;
  onUpdated: () => void;
  /** When false, Ctrl+S does not submit (e.g. another tab is active). Default true. */
  saveShortcutEnabled?: boolean;
  restorePending?: boolean;
  developmentHardDeletePending?: boolean;
  onArchiveSuccess?: () => void;
  onPermanentDeleteSuccess?: () => void;
  onRestore?: () => void | Promise<void>;
  onDevelopmentHardDelete?: () => void | Promise<void>;
};

type SettingsFormValues = {
  name: string;
  email?: string;
  phone?: string;
  address?: string;
  status?: string;
};

export function TenantDetailSettingsTab({
  tenant,
  onUpdated,
  saveShortcutEnabled = true,
  restorePending,
  developmentHardDeletePending,
  onArchiveSuccess,
  onPermanentDeleteSuccess,
  onRestore,
  onDevelopmentHardDelete,
}: TenantDetailSettingsTabProps) {
  const { message } = useAntdApp();

  const { t } = useI18n();
  const canManageDeletion = useCanManageTenantDeletion();
  const [form] = Form.useForm<SettingsFormValues>();

  const saveMutation = useMutation({
    mutationFn: (values: SettingsFormValues) =>
      updateAdminTenant(tenant.id, {
        name: values.name,
        email: values.email,
        phone: values.phone,
        address: values.address,
        status: values.status,
      }),
    onSuccess: () => {
      message.success(t('tenants.messages.updated'));
      onUpdated();
    },
    onError: () => message.error(t('tenants.messages.saveFailed')),
  });

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Card>
        <Form
          key={`${tenant.id}-${tenant.updatedAt ?? tenant.createdAt}`}
          form={form}
          layout="vertical"
          initialValues={{
            name: tenant.name,
            email: tenant.email ?? undefined,
            phone: tenant.phone ?? undefined,
            address: tenant.address ?? undefined,
            status: tenant.status === 'deleted' ? undefined : tenant.status,
          }}
          onFinish={(values) => saveMutation.mutate(values)}
        >
          <Form.Item
            name="name"
            label={t('tenants.fields.name')}
            rules={[{ required: true, message: t('tenants.validation.nameRequired') }]}
          >
            <Input disabled={tenant.status === 'deleted'} />
          </Form.Item>
          <Form.Item label={t('tenants.fields.slug')}>
            <Input value={tenant.slug} disabled />
          </Form.Item>
          <Form.Item name="email" label={t('tenants.fields.email')}>
            <Input type="email" disabled={tenant.status === 'deleted'} />
          </Form.Item>
          <Form.Item name="phone" label={t('tenants.fields.phone')}>
            <Input disabled={tenant.status === 'deleted'} />
          </Form.Item>
          <Form.Item name="address" label={t('tenants.fields.address')}>
            <Input.TextArea rows={2} disabled={tenant.status === 'deleted'} />
          </Form.Item>
          {tenant.status !== 'deleted' ? (
            <Form.Item name="status" label={t('tenants.fields.status')}>
              <Select
                options={[
                  { value: 'active', label: t('tenants.status.active') },
                  { value: 'suspended', label: t('tenants.status.suspended') },
                ]}
              />
            </Form.Item>
          ) : null}
          {tenant.status !== 'deleted' ? (
            <SaveButton
              htmlType="submit"
              loading={saveMutation.isPending}
              shortcutEnabled={saveShortcutEnabled}
              showShortcutInLabel={false}
            >
              {t('tenants.detail.settings.save')}
            </SaveButton>
          ) : null}
        </Form>
      </Card>

      {tenant.status !== 'deleted' ? (
        <TenantOperationModeCard tenant={tenant} onUpdated={onUpdated} />
      ) : null}

      {canManageDeletion && onArchiveSuccess && onPermanentDeleteSuccess && onRestore ? (
        <TenantDetailDangerZone
          tenant={tenant}
          restorePending={restorePending}
          developmentHardDeletePending={developmentHardDeletePending}
          onArchiveSuccess={onArchiveSuccess}
          onPermanentDeleteSuccess={onPermanentDeleteSuccess}
          onRestore={onRestore}
          onDevelopmentHardDelete={onDevelopmentHardDelete}
        />
      ) : null}
    </Space>
  );
}
