'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import { Button, Card, Form, Input, Select, Space } from 'antd';
import { useMutation } from '@tanstack/react-query';

import {
    updateAdminTenant,
    type AdminTenantDetail,
} from '@/features/super-admin/api/adminTenants';
import { TenantDetailDangerZone } from '@/features/super-admin/components/TenantDetailDangerZone';
import { useCanManageTenantDeletion } from '@/features/super-admin/hooks/useCanManageTenantDeletion';
import { useI18n } from '@/i18n';

export type TenantDetailSettingsTabProps = {
    tenant: AdminTenantDetail;
    onUpdated: () => void;
    softDeletePending?: boolean;
    restorePending?: boolean;
    hardDeletePending?: boolean;
    developmentHardDeletePending?: boolean;
    onSoftDelete?: () => void | Promise<void>;
    onRestore?: () => void | Promise<void>;
    onHardDelete?: (confirmSlug: string) => void | Promise<void>;
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
    softDeletePending,
    restorePending,
    hardDeletePending,
    developmentHardDeletePending,
    onSoftDelete,
    onRestore,
    onHardDelete,
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
                    <Form.Item name="name" label={t('tenants.fields.name')} rules={[{ required: true }]}>
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
                        <Button type="primary" htmlType="submit" loading={saveMutation.isPending}>
                            {t('tenants.detail.settings.save')}
                        </Button>
                    ) : null}
                </Form>
            </Card>

            {canManageDeletion && onSoftDelete && onRestore && onHardDelete ? (
                <TenantDetailDangerZone
                    tenant={tenant}
                    softDeletePending={softDeletePending}
                    restorePending={restorePending}
                    hardDeletePending={hardDeletePending}
                    developmentHardDeletePending={developmentHardDeletePending}
                    onSoftDelete={onSoftDelete}
                    onRestore={onRestore}
                    onHardDelete={onHardDelete}
                    onDevelopmentHardDelete={onDevelopmentHardDelete}
                />
            ) : null}
        </Space>
    );
}
