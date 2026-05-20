'use client';

import { Alert, Form, Input, Modal, Select, Switch, Typography } from 'antd';
import { useEffect } from 'react';

import { INVITE_TENANT_ROLES } from '@/features/super-admin/api/tenantUsers';
import { useI18n } from '@/i18n';

export type InviteUserFormValues = {
    email: string;
    role: string;
    isOwner: boolean;
};

export type InviteUserModalProps = {
    open: boolean;
    confirmLoading?: boolean;
    onClose: () => void;
    onSubmit: (values: InviteUserFormValues) => void;
};

export function InviteUserModal({ open, confirmLoading, onClose, onSubmit }: InviteUserModalProps) {
    const { t } = useI18n();
    const [form] = Form.useForm<InviteUserFormValues>();

    useEffect(() => {
        if (!open) {
            form.resetFields();
        }
    }, [open, form]);

    return (
        <Modal
            title={t('tenants.users.invite.title')}
            open={open}
            onCancel={onClose}
            onOk={() => form.submit()}
            confirmLoading={confirmLoading}
            destroyOnClose
        >
            <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
                {t('tenants.users.invite.subtitle')}
            </Typography.Paragraph>
            <Form
                form={form}
                layout="vertical"
                initialValues={{ role: 'Manager', isOwner: false }}
                onFinish={onSubmit}
            >
                <Form.Item
                    name="email"
                    label={t('tenants.users.invite.email')}
                    rules={[
                        { required: true, message: t('tenants.users.invite.emailRequired') },
                        { type: 'email', message: t('tenants.users.invite.emailInvalid') },
                    ]}
                >
                    <Input type="email" placeholder="admin@beispiel.regkasse.at" />
                </Form.Item>
                <Form.Item name="role" label={t('tenants.users.invite.role')} rules={[{ required: true }]}>
                    <Select
                        options={INVITE_TENANT_ROLES.map((role) => ({
                            value: role,
                            label: role,
                        }))}
                    />
                </Form.Item>
                <Form.Item name="isOwner" label={t('tenants.users.invite.isOwner')} valuePropName="checked">
                    <Switch />
                </Form.Item>
                <Alert type="info" showIcon message={t('tenants.users.invite.smtpHint')} />
            </Form>
        </Modal>
    );
}
