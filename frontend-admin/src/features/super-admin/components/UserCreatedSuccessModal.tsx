'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useEffect, useState } from 'react';
import { Modal, Alert, Button, Space, Typography } from 'antd';

import { CredentialCopyRow } from '@/features/super-admin/components/CredentialCopyRow';
import type { CreateTenantUserResult } from '@/features/super-admin/api/tenantUsers';
import { useSuperAdminPlatformPolicy } from '@/features/super-admin/auth/superAdminPlatformPolicy';
import { useI18n } from '@/i18n';
import { copyTextToClipboard } from '@/lib/clipboard';

export type UserCreatedSuccessModalProps = {
    open: boolean;
    result: CreateTenantUserResult | null;
    onClose: () => void;
};

export function UserCreatedSuccessModal({ open, result, onClose }: UserCreatedSuccessModalProps) {
  const { message } = useAntdApp();

    const { t } = useI18n();
    const { canProvisionTenantCredentials } = useSuperAdminPlatformPolicy();
    const [password, setPassword] = useState('');

    useEffect(() => {
        if (open && result?.generatedPassword) {
            setPassword(result.generatedPassword);
            return;
        }

        setPassword('');
    }, [open, result]);

    if (!canProvisionTenantCredentials) {
        return null;
    }

    if (!result?.success) return null;

    const portalUrl = result.tenantPortalUrl ?? '';
    const userName = result.userName?.trim();

    const copyAllCredentials = async () => {
        const lines = [
            userName ? `${t('tenants.users.quick.result.usernameLabel')} ${userName}` : null,
            `${t('tenants.users.quick.result.emailLabel')} ${result.email}`,
            `${t('users.create.password')} ${password}`,
        ].filter(Boolean) as string[];
        const copied = await copyTextToClipboard(lines.join('\n'));
        if (copied) {
            message.success(t('tenants.provisioning.copySuccess'));
        } else {
            message.error(t('tenants.provisioning.copyFailed'));
        }
    };

    return (
        <Modal
            title={t('users.create.success')}
            open={open}
            onCancel={onClose}
            destroyOnHidden
            footer={[
                <Button key="copy-all" onClick={() => void copyAllCredentials()}>
                    {t('tenants.users.quick.result.copyAll')}
                </Button>,
                <Button key="done" type="primary" onClick={onClose}>
                    {t('common.ok', { defaultValue: 'OK' })}
                </Button>,
            ]}
            width={520}
        >
            <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                {userName ? (
                    <CredentialCopyRow label={t('tenants.users.quick.result.usernameLabel')} value={userName} />
                ) : null}
                <CredentialCopyRow label={t('tenants.users.quick.result.emailLabel')} value={result.email} />
                <CredentialCopyRow label={t('users.create.password')} value={password} />
                <Alert type="warning" showIcon title={t('users.create.generatedPasswordInfo')} />
                {portalUrl ? (
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {portalUrl}
                    </Typography.Text>
                ) : null}
            </Space>
        </Modal>
    );
}
