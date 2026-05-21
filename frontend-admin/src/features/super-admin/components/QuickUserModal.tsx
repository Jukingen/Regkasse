'use client';

import { useEffect, useMemo } from 'react';
import { Alert, Form, Modal, Select, Typography } from 'antd';
import { ThunderboltOutlined } from '@ant-design/icons';

import { QUICK_USER_ROLES } from '@/features/super-admin/api/quickUser';
import { useI18n } from '@/i18n';

export type QuickUserFormValues = {
    role: string;
};

export type QuickUserModalProps = {
    open: boolean;
    confirmLoading?: boolean;
    tenantSlug: string;
    tenantName?: string;
    onClose: () => void;
    onSubmit: (values: QuickUserFormValues) => void;
};

export function QuickUserModal({
    open,
    confirmLoading,
    tenantSlug,
    tenantName,
    onClose,
    onSubmit,
}: QuickUserModalProps) {
    const { t } = useI18n();
    const [form] = Form.useForm<QuickUserFormValues>();

    const roleOptions = useMemo(
        () =>
            QUICK_USER_ROLES.map((role) => ({
                value: role,
                label: t(`users.invite.roleOptions.${role}.label`),
            })),
        [t],
    );

    useEffect(() => {
        if (!open) {
            form.resetFields();
            return;
        }
        form.setFieldsValue({ role: 'Manager' });
    }, [open, form]);

    const watchedRole = Form.useWatch('role', form) ?? 'Manager';

    const infoEmailExample = t('tenants.users.quick.emailPreview', {
        role: watchedRole.toLowerCase(),
        random: 'a3f9k2',
        slug: tenantSlug,
    });

    return (
        <Modal
            title={
                <span>
                    <ThunderboltOutlined style={{ marginRight: 8 }} />
                    {t('tenants.users.quick.title')}
                </span>
            }
            open={open}
            onCancel={onClose}
            onOk={() => form.submit()}
            okText={t('tenants.users.quick.generate')}
            cancelText={t('common.buttons.cancel')}
            confirmLoading={confirmLoading}
            destroyOnClose
        >
            <Form form={form} layout="vertical" onFinish={onSubmit}>
                {tenantName ? (
                    <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
                        {tenantName} ({tenantSlug})
                    </Typography.Paragraph>
                ) : null}
                <Form.Item name="role" label={t('tenants.users.quick.role')} rules={[{ required: true }]}>
                    <Select options={roleOptions} />
                </Form.Item>
                <Alert
                    type="info"
                    showIcon
                    message={t('tenants.users.quick.autoTitle')}
                    description={
                        <ul style={{ margin: '8px 0 0', paddingLeft: 20 }}>
                            <li>{infoEmailExample}</li>
                            <li>{t('tenants.users.quick.autoPassword')}</li>
                            <li>{t('tenants.users.quick.autoForceChange')}</li>
                        </ul>
                    }
                />
            </Form>
        </Modal>
    );
}
