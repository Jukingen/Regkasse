'use client';

import React, { useEffect, useMemo } from 'react';
import { Alert, Form, Input, Modal, Select } from 'antd';
import type { Rule } from 'antd/es/form';

import type { UserInfo } from '@/features/users/api/usersGateway';
import { usersCopy } from '@/features/users/constants/copy';
import { useI18n } from '@/i18n';

function fullName(record: UserInfo): string {
    const first = record.firstName ?? '';
    const last = record.lastName ?? '';
    const name = `${first} ${last}`.trim();
    return name || record.userName || record.id || '—';
}

type DeactivateUserModalProps = {
    user: UserInfo;
    onCancel: () => void;
    onConfirm: (reason: string) => void;
    confirmLoading?: boolean;
    reasonRules: Rule[];
};

export function DeactivateUserModal({
    user,
    onCancel,
    onConfirm,
    confirmLoading,
    reasonRules,
}: DeactivateUserModalProps) {
    const [form] = Form.useForm<{ reason: string }>();

    const handleOk = () => {
        void form.validateFields().then(
            (values) => onConfirm(values.reason),
            () => { /* validation shown on form */ },
        );
    };

    const handleCancel = () => {
        form.resetFields();
        onCancel();
    };

    return (
        <Modal
            title={usersCopy.deactivateUser}
            open
            onOk={handleOk}
            onCancel={handleCancel}
            okText={usersCopy.okDeactivate}
            okButtonProps={{ danger: true }}
            confirmLoading={confirmLoading}
        >
            <p style={{ marginBottom: 16 }}>
                <strong>{fullName(user)}</strong> ({user.email ?? user.userName}) {usersCopy.confirmDeactivate}
            </p>
            <Form form={form} layout="vertical">
                <Form.Item name="reason" label={usersCopy.reasonRequired} rules={reasonRules}>
                    <Input.TextArea rows={3} placeholder={usersCopy.reasonPlaceholder} maxLength={500} showCount />
                </Form.Item>
            </Form>
        </Modal>
    );
}

type ResetPasswordUserModalProps = {
    user: UserInfo;
    onCancel: () => void;
    onConfirm: (newPassword: string) => void;
    confirmLoading?: boolean;
    passwordRules: Rule[];
    validationError?: string | null;
    onClearValidationError?: () => void;
};

export function ResetPasswordUserModal({
    user,
    onCancel,
    onConfirm,
    confirmLoading,
    passwordRules,
    validationError,
    onClearValidationError,
}: ResetPasswordUserModalProps) {
    const [form] = Form.useForm<{ newPassword: string }>();

    useEffect(() => {
        form.resetFields();
        onClearValidationError?.();
    }, [user.id, form, onClearValidationError]);

    useEffect(() => {
        if (validationError) {
            form.setFields([{ name: 'newPassword', errors: [validationError] }]);
        }
    }, [validationError, form]);

    const handleOk = () => {
        void form.validateFields()
            .then((values) => onConfirm(values.newPassword))
            .catch(() => { /* validation shown on form */ });
    };

    const handleCancel = () => {
        form.resetFields();
        onClearValidationError?.();
        onCancel();
    };

    return (
        <Modal
            title={usersCopy.resetPasswordUser}
            open
            onOk={handleOk}
            onCancel={handleCancel}
            okText={usersCopy.save}
            confirmLoading={confirmLoading}
        >
            <p style={{ marginBottom: 8 }}>
                <strong>{fullName(user)}</strong> ({user.userName})
            </p>
            <Alert
                type="info"
                title={usersCopy.resetPasswordSecurityNote}
                showIcon
                style={{ marginBottom: 16 }}
            />
            {validationError ? (
                <Alert type="error" title={validationError} showIcon style={{ marginBottom: 16 }} />
            ) : null}
            <Form form={form} layout="vertical">
                <Form.Item name="newPassword" label={usersCopy.newPassword} rules={passwordRules}>
                    <Input.Password placeholder="••••••••" autoComplete="new-password" />
                </Form.Item>
            </Form>
        </Modal>
    );
}

type CreateRoleModalProps = {
    onCancel: () => void;
    onConfirm: (payload: { name: string; inheritFromRole?: string }) => void;
    confirmLoading?: boolean;
    roleNameRules: Rule[];
    inheritRoleOptions?: { value: string; label: string }[];
};

export function CreateRoleModal({
    onCancel,
    onConfirm,
    confirmLoading,
    roleNameRules,
    inheritRoleOptions = [],
}: CreateRoleModalProps) {
    const { t } = useI18n();
    const [form] = Form.useForm<{ name: string; inheritFromRole?: string }>();

    const selectableInheritOptions = useMemo(
        () => inheritRoleOptions.filter((option) => option.value !== 'SuperAdmin'),
        [inheritRoleOptions],
    );

    const handleOk = () => {
        void form
            .validateFields()
            .then((values) =>
                onConfirm({
                    name: values.name.trim(),
                    inheritFromRole: values.inheritFromRole?.trim() || undefined,
                }),
            )
            .catch(() => {
                /* validation shown on form */
            });
    };

    const handleCancel = () => {
        form.resetFields();
        onCancel();
    };

    return (
        <Modal
            title={usersCopy.createRole}
            open
            destroyOnHidden
            onOk={handleOk}
            onCancel={handleCancel}
            afterClose={() => form.resetFields()}
            okText={usersCopy.save}
            confirmLoading={confirmLoading}
        >
            <Form form={form} layout="vertical">
                <Form.Item name="name" label={usersCopy.roleName} rules={roleNameRules}>
                    <Input placeholder="z. B. Manager" maxLength={50} showCount autoComplete="off" />
                </Form.Item>
                {selectableInheritOptions.length > 0 ? (
                    <Form.Item
                        name="inheritFromRole"
                        label={t('users.createRole.inheritFromRole')}
                        extra={t('users.createRole.inheritFromRoleHelp')}
                    >
                        <Select
                            allowClear
                            placeholder={t('users.createRole.inheritFromRolePlaceholder')}
                            options={selectableInheritOptions}
                        />
                    </Form.Item>
                ) : null}
            </Form>
        </Modal>
    );
}
