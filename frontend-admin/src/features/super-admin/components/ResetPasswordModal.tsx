'use client';

import { useEffect, useState } from 'react';
import { Alert, Button, Checkbox, Form, Input, Modal, Space, Typography, message } from 'antd';
import { CopyOutlined } from '@ant-design/icons';

import type { TenantUser } from '@/features/super-admin/api/tenantUsers';
import {
    resetTenantUserPassword,
    type TenantUserPasswordResetResult,
} from '@/features/super-admin/api/tenantUsers';
import { useI18n } from '@/i18n';

export type ResetPasswordModalProps = {
    open: boolean;
    tenantId: string;
    user: TenantUser | null;
    smtpConfigured?: boolean;
    onClose: () => void;
    onCompleted?: () => void;
};

type ConfirmFormValues = {
    sendEmail: boolean;
};

export function ResetPasswordModal({
    open,
    tenantId,
    user,
    smtpConfigured = false,
    onClose,
    onCompleted,
}: ResetPasswordModalProps) {
    const { t } = useI18n();
    const [form] = Form.useForm<ConfirmFormValues>();
    const [confirming, setConfirming] = useState(false);
    const [result, setResult] = useState<TenantUserPasswordResetResult | null>(null);

    useEffect(() => {
        if (open) {
            form.setFieldsValue({ sendEmail: smtpConfigured });
            setResult(null);
        }
    }, [open, smtpConfigured, form]);

    const handleConfirm = async () => {
        if (!user) return;
        const values = await form.validateFields();
        setConfirming(true);
        try {
            const res = await resetTenantUserPassword(tenantId, user.userId, {
                sendEmail: values.sendEmail,
            });
            setResult(res);
            onCompleted?.();
            if (res.emailSent) {
                message.success(t('tenants.users.resetPassword.emailSent'));
            } else {
                message.success(t('tenants.users.messages.passwordReset'));
            }
        } catch {
            message.error(t('tenants.users.messages.passwordResetFailed'));
        } finally {
            setConfirming(false);
        }
    };

    const copyPassword = async () => {
        if (!result?.generatedPassword) return;
        try {
            await navigator.clipboard.writeText(result.generatedPassword);
            message.success(t('tenants.provisioning.copySuccess'));
        } catch {
            message.error(t('tenants.provisioning.copyFailed'));
        }
    };

    const handleClose = () => {
        setResult(null);
        onClose();
    };

    return (
        <Modal
            title={t('tenants.users.resetPassword.title')}
            open={open}
            onCancel={handleClose}
            destroyOnClose
            footer={
                result
                    ? [
                          <Button key="ok" type="primary" onClick={handleClose}>
                              {t('common.ok', { defaultValue: 'OK' })}
                          </Button>,
                      ]
                    : [
                          <Button key="cancel" onClick={handleClose}>
                              {t('common.cancel', { defaultValue: 'Abbrechen' })}
                          </Button>,
                          <Button key="reset" type="primary" danger loading={confirming} onClick={() => void handleConfirm()}>
                              {t('tenants.users.resetPassword.confirm')}
                          </Button>,
                      ]
            }
        >
            {result ? (
                <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    <Typography.Text type="secondary">{result.deliveryNote}</Typography.Text>
                    {result.emailSent ? (
                        <Alert type="success" showIcon message={t('tenants.users.resetPassword.emailSent')} />
                    ) : null}
                    {result.forcePasswordChangeOnNextLogin ? (
                        <Alert type="info" showIcon message={t('tenants.users.resetPassword.forceChangeHint')} />
                    ) : null}
                    {result.generatedPassword ? (
                        <Alert
                            type="warning"
                            showIcon
                            message={t('tenants.users.invite.passwordOnce')}
                            description={
                                <Space>
                                    <Typography.Text code>{result.generatedPassword}</Typography.Text>
                                    <Button size="small" icon={<CopyOutlined />} onClick={() => void copyPassword()}>
                                        {t('tenants.provisioning.copyPassword')}
                                    </Button>
                                </Space>
                            }
                        />
                    ) : null}
                </Space>
            ) : user ? (
                <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    <Typography.Text>
                        {t('tenants.users.resetPassword.body', {
                            name: user.name,
                            email: user.email,
                        })}
                    </Typography.Text>
                    <Form form={form} layout="vertical" initialValues={{ sendEmail: smtpConfigured }}>
                        <Form.Item name="sendEmail" valuePropName="checked">
                            <Checkbox disabled={!smtpConfigured}>
                                {t('tenants.users.resetPassword.sendEmail')}
                            </Checkbox>
                        </Form.Item>
                    </Form>
                    {!smtpConfigured ? (
                        <Typography.Text type="secondary">{t('tenants.users.resetPassword.smtpOff')}</Typography.Text>
                    ) : null}
                    <Form.Item label={t('tenants.users.resetPassword.preview')}>
                        <Input disabled placeholder={t('tenants.users.resetPassword.previewHint')} />
                    </Form.Item>
                </Space>
            ) : null}
        </Modal>
    );
}
