'use client';

import { Form, Modal, Select, Switch, Typography } from 'antd';
import { useEffect } from 'react';

import { ASSIGNABLE_ROLES } from '@/features/super-admin/components/TenantUserTable';
import { useI18n } from '@/i18n';

export type AddExistingUserFormValues = {
    userId: string;
    role: string;
    isOwner: boolean;
};

export type AddExistingUserModalProps = {
    open: boolean;
    confirmLoading?: boolean;
    loadingUsers?: boolean;
    userOptions: { value: string; label: string }[];
    onClose: () => void;
    onSubmit: (values: AddExistingUserFormValues) => void;
};

export function AddExistingUserModal({
    open,
    confirmLoading,
    loadingUsers,
    userOptions,
    onClose,
    onSubmit,
}: AddExistingUserModalProps) {
    const { t } = useI18n();
    const [form] = Form.useForm<AddExistingUserFormValues>();

    useEffect(() => {
        if (!open) form.resetFields();
    }, [open, form]);

    return (
        <Modal
            title={t('tenants.users.add.title')}
            open={open}
            onCancel={onClose}
            onOk={() => form.submit()}
            confirmLoading={confirmLoading}
            destroyOnHidden
        >
            <Form
                form={form}
                layout="vertical"
                initialValues={{ role: 'Manager', isOwner: false }}
                onFinish={onSubmit}
            >
                <Form.Item
                    name="userId"
                    label={t('tenants.users.add.user')}
                    rules={[{ required: true, message: t('tenants.users.add.userRequired') }]}
                >
                    <Select
                        showSearch
                        optionFilterProp="label"
                        loading={loadingUsers}
                        options={userOptions}
                        placeholder={t('tenants.users.add.userPlaceholder')}
                    />
                </Form.Item>
                <Form.Item name="role" label={t('tenants.users.add.role')} rules={[{ required: true }]}>
                    <Select
                        options={ASSIGNABLE_ROLES.map((role) => ({
                            value: role,
                            label: role,
                        }))}
                    />
                </Form.Item>
                <Form.Item name="isOwner" label={t('tenants.users.add.isOwner')} valuePropName="checked">
                    <Switch />
                </Form.Item>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    {t('tenants.users.add.ownerHint')}
                </Typography.Paragraph>
            </Form>
        </Modal>
    );
}
