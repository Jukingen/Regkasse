'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useEffect, useState } from 'react';
import { Modal, Alert, Button, Descriptions, Space, Typography } from 'antd';
import { CheckCircleOutlined } from '@ant-design/icons';

import { CredentialCopyRow } from '@/features/super-admin/components/CredentialCopyRow';
import type { CreateQuickUserResult } from '@/features/super-admin/api/quickUser';
import { useSuperAdminPlatformPolicy } from '@/features/super-admin/auth/superAdminPlatformPolicy';
import { useI18n } from '@/i18n';
import { copyTextToClipboard } from '@/lib/clipboard';

export type QuickUserSuccessModalProps = {
    open: boolean;
    result: CreateQuickUserResult | null;
    role: string;
    tenantName: string;
    tenantSlug: string;
    onClose: () => void;
    onGenerateAnother: () => void;
};

export function QuickUserSuccessModal({
    open,
    result,
    role,
    tenantName,
    tenantSlug,
    onClose,
    onGenerateAnother,
}: QuickUserSuccessModalProps) {
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

    if (!canProvisionTenantCredentials || !result?.success) {
        return null;
    }

    const portalUrl = result.tenantPortalUrl ?? `https://${tenantSlug}.regkasse.at`;
    const userName = result.userName?.trim() || result.email;
    const displayRole = result.role ?? role;
    const roleLabelKey = `users.create.roleOptions.${displayRole}.label` as const;
    const roleLabel = t(roleLabelKey) !== roleLabelKey ? t(roleLabelKey) : displayRole;

    const copyAllCredentials = async () => {
        const block = [
            `${t('tenants.users.quick.result.usernameLabel')} ${userName}`,
            `${t('tenants.users.quick.result.emailLabel')} ${result.email}`,
            `${t('tenants.users.quick.result.passwordLabel')} ${password}`,
        ].join('\n');
        const copied = await copyTextToClipboard(block);
        if (copied) {
            message.success(t('tenants.provisioning.copySuccess'));
        } else {
            message.error(t('tenants.provisioning.copyFailed'));
        }
    };

    return (
        <Modal
            title={
                <Space>
                    <CheckCircleOutlined style={{ color: '#52c41a' }} />
                    {t('tenants.users.quick.result.title')}
                </Space>
            }
            open={open}
            onCancel={onClose}
            destroyOnHidden
            footer={[
                <Button key="copy-all" onClick={() => void copyAllCredentials()}>
                    {t('tenants.users.quick.result.copyAll')}
                </Button>,
                <Button key="another" onClick={onGenerateAnother}>
                    {t('tenants.users.quick.result.generateAnother')}
                </Button>,
                <Button key="done" type="primary" onClick={onClose}>
                    {t('tenants.users.quick.result.done')}
                </Button>,
            ]}
            width={560}
        >
            <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                <CredentialCopyRow label={t('tenants.users.quick.result.usernameLabel')} value={userName} />
                <CredentialCopyRow label={t('tenants.users.quick.result.emailLabel')} value={result.email} />
                <CredentialCopyRow label={t('tenants.users.quick.result.passwordLabel')} value={password} />

                <Alert type="warning" showIcon title={t('tenants.users.quick.result.passwordOnceWarning')} />

                <Descriptions column={1} size="small" colon={false}>
                    <Descriptions.Item label={t('tenants.users.quick.result.roleLabel')}>
                        {roleLabel}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('tenants.users.quick.result.tenantLabel')}>
                        {t('tenants.users.quick.result.tenantValue', { name: tenantName, slug: tenantSlug })}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('tenants.users.quick.result.loginUrlLabel')}>
                        <Typography.Link href={portalUrl} target="_blank" rel="noopener noreferrer">
                            {portalUrl}
                        </Typography.Link>
                    </Descriptions.Item>
                </Descriptions>

                <div>
                    <Typography.Text strong>{t('tenants.users.quick.result.nextStepsTitle')}</Typography.Text>
                    <Typography.Paragraph style={{ marginBottom: 0, marginTop: 8 }}>
                        <ol style={{ paddingLeft: 20, margin: 0 }}>
                            <li>{t('tenants.users.quick.result.nextStep1')}</li>
                            <li>{t('tenants.users.quick.result.nextStep2')}</li>
                        </ol>
                    </Typography.Paragraph>
                </div>
            </Space>
        </Modal>
    );
}
