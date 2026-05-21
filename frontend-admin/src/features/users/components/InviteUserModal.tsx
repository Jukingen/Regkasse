'use client';

import React, { useEffect, useMemo } from 'react';
import { Alert, Form, Input, Modal, Select, Switch, Typography } from 'antd';

import { INVITE_TENANT_ROLES } from '@/features/super-admin/api/tenantUsers';
import { useI18n } from '@/i18n';

export type InviteUserFormValues = {
    email: string;
    role: string;
    isOwner: boolean;
    tenantId?: string;
};

export type TenantOption = {
    value: string;
    label: string;
};

export type InviteUserModalProps = {
    open: boolean;
    confirmLoading?: boolean;
    onClose: () => void;
    onSubmit: (values: InviteUserFormValues) => void;
    fixedTenantId?: string;
    tenantOptions?: TenantOption[];
    showOwnerToggle?: boolean;
    variant?: 'tenantDetail' | 'usersPage';
    initialValues?: Partial<InviteUserFormValues>;
};

const USERS_PAGE_ROLES = ['Manager', 'Cashier', 'Accountant'] as const;

export function InviteUserModal({
    open,
    confirmLoading,
    onClose,
    onSubmit,
    fixedTenantId,
    tenantOptions = [],
    showOwnerToggle = false,
    variant = 'usersPage',
    initialValues,
}: InviteUserModalProps) {
    const { t } = useI18n();
    const [form] = Form.useForm<InviteUserFormValues>();
    const isUsersPage = variant === 'usersPage';

    const roleOptions = useMemo(() => {
        const roles = isUsersPage ? USERS_PAGE_ROLES : INVITE_TENANT_ROLES;
        return roles.map((role) => ({
            value: role,
            label: isUsersPage ? t(`users.invite.roleOptions.${role}.label`) : role,
        }));
    }, [isUsersPage, t]);

    useEffect(() => {
        if (!open) {
            form.resetFields();
            return;
        }
        form.setFieldsValue({
            role: 'Manager',
            isOwner: false,
            tenantId: fixedTenantId,
            ...initialValues,
        });
    }, [open, form, fixedTenantId, initialValues]);

    const handleFinish = (values: InviteUserFormValues) => {
        const tenantId = fixedTenantId ?? values.tenantId;
        onSubmit({
            ...values,
            tenantId,
            isOwner: showOwnerToggle ? Boolean(values.isOwner) : false,
        });
    };

    return (
        <Modal
            title={t('users.invite.title')}
            open={open}
            onCancel={onClose}
            onOk={() => form.submit()}
            okText={t('users.invite.send')}
            cancelText={t('common.buttons.cancel')}
            confirmLoading={confirmLoading}
            destroyOnClose
        >
            <Form form={form} layout="vertical" onFinish={handleFinish}>
                <Form.Item
                    name="email"
                    label={t('users.invite.email')}
                    rules={[
                        { required: true, message: t('tenants.users.invite.emailRequired') },
                        { type: 'email', message: t('tenants.users.invite.emailInvalid') },
                    ]}
                >
                    <Input type="email" placeholder="neuer@mitarbeiter.cafe.at" />
                </Form.Item>
                {!fixedTenantId && tenantOptions.length > 0 ? (
                    <Form.Item
                        name="tenantId"
                        label={t('users.invite.tenant')}
                        rules={[{ required: true, message: t('users.invite.tenantRequired') }]}
                    >
                        <Select
                            showSearch
                            optionFilterProp="label"
                            placeholder={t('users.invite.tenantPlaceholder')}
                            options={tenantOptions}
                        />
                    </Form.Item>
                ) : null}
                <Form.Item name="role" label={t('users.invite.role')} rules={[{ required: true }]}>
                    <Select options={roleOptions} />
                </Form.Item>
                <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginBottom: 16 }}>
                    {t('users.invite.emailHint')}
                </Typography.Paragraph>
                {showOwnerToggle ? (
                    <Form.Item name="isOwner" label={t('tenants.users.invite.isOwner')} valuePropName="checked">
                        <Switch />
                    </Form.Item>
                ) : null}
                {!isUsersPage ? (
                    <Alert type="info" showIcon message={t('tenants.users.invite.smtpHint')} />
                ) : null}
            </Form>
        </Modal>
    );
}
