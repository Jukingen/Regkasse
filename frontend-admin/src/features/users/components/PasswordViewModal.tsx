'use client';

import { useEffect, useState } from 'react';
import { Alert, Button, Input, Modal, Space, message } from 'antd';
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
            message.error('Kein Passwort zum Kopieren vorhanden');
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
            title={`Passwort für ${userEmail}`}
            open={open}
            onCancel={onClose}
            destroyOnHidden
            footer={[
                <Button key="close" onClick={onClose}>
                    Schließen
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
            <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                <Alert
                    message="Sicherheitshinweis"
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
                        Temporäres Passwort generieren
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
