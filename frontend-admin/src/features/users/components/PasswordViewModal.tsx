'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useEffect, useState } from 'react';
import { Modal, Alert, Button, Input, Space } from 'antd';
import { CopyOutlined } from '@ant-design/icons';

import { useGenerateTemporaryPasswordMutation } from '@/features/users/api/usersGateway';
import { useI18n } from '@/i18n';
import { copyTextToClipboard } from '@/lib/clipboard';

export type PasswordViewModalProps = {
    open: boolean;
    userId: string;
    userEmail: string;
    onClose: () => void;
};

export function PasswordViewModal({
    open,
    userId,
    userEmail,
    onClose,
}: PasswordViewModalProps) {
  const { message } = useAntdApp();

    const { t } = useI18n();
    const [password, setPassword] = useState('');
    const generateTemporaryPasswordMutation = useGenerateTemporaryPasswordMutation();

    useEffect(() => {
        if (open) {
            setPassword('');
        }
    }, [open]);

    const handleGenerate = async () => {
        if (!userId) return;
        try {
            const result = await generateTemporaryPasswordMutation.mutateAsync(userId);
            setPassword(result.generatedPassword ?? '');
            message.success(t('tenants.users.messages.passwordReset'));
        } catch {
            message.error(t('tenants.users.messages.passwordResetFailed'));
        }
    };

    const handleCopy = async () => {
        if (!password) {
            message.error(t('users.password.noPasswordToCopy'));
            return;
        }

        const copied = await copyTextToClipboard(password);
        if (copied) {
            message.success(t('tenants.provisioning.copySuccess'));
        } else {
            message.error(t('tenants.provisioning.copyFailed'));
        }
    };

    return (
        <Modal
            title={t('users.password.titleForEmail', { email: userEmail })}
            open={open}
            onCancel={onClose}
            destroyOnHidden
            footer={[
                <Button key="close" onClick={onClose}>
                    {t('common.buttons.close')}
                </Button>,
                <Button
                    key="copy"
                    type="primary"
                    icon={<CopyOutlined />}
                    onClick={() => void handleCopy()}
                    disabled={!password}
                >
                    {t('tenants.provisioning.copyPassword')}
                </Button>,
            ]}
        >
            <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                <Alert
                    title={t('users.create.passwordWarningTitle')}
                    description={t('users.create.generatedPasswordInfo')}
                    type="warning"
                    showIcon
                />

                {!password ? (
                    <Button
                        type="primary"
                        onClick={() => void handleGenerate()}
                        loading={generateTemporaryPasswordMutation.isPending}
                    >
                        {t('users.password.generateTemporary')}
                    </Button>
                ) : (
                    <Space.Compact style={{ width: '100%' }}>
                        <Input
                            value={password}
                            readOnly
                            style={{ fontFamily: 'monospace', fontSize: 16 }}
                        />
                        <Button onClick={() => void handleCopy()}>
                            {t('tenants.provisioning.copyPassword')}
                        </Button>
                    </Space.Compact>
                )}
            </Space>
        </Modal>
    );
}
