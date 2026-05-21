'use client';

import { Alert, Button, Modal, Space, Typography, message } from 'antd';
import { CopyOutlined } from '@ant-design/icons';

import type { CreateTenantUserResult } from '@/features/super-admin/api/tenantUsers';
import { useSuperAdminPlatformPolicy } from '@/features/super-admin/auth/superAdminPlatformPolicy';
import { useI18n } from '@/i18n';

export type UserCreatedSuccessModalProps = {
    open: boolean;
    result: CreateTenantUserResult | null;
    onClose: () => void;
};

export function UserCreatedSuccessModal({ open, result, onClose }: UserCreatedSuccessModalProps) {
    const { t } = useI18n();
    const { canProvisionTenantCredentials } = useSuperAdminPlatformPolicy();

    if (!canProvisionTenantCredentials) {
        return null;
    }

    const copyPassword = async () => {
        if (!result?.generatedPassword) return;
        try {
            await navigator.clipboard.writeText(result.generatedPassword);
            message.success(t('tenants.provisioning.copySuccess'));
        } catch {
            message.error(t('tenants.provisioning.copyFailed'));
        }
    };

    if (!result?.success) return null;

    const portalUrl = result.tenantPortalUrl ?? '';

    return (
        <Modal
            title={t('tenants.users.invite.resultSuccessTitle')}
            open={open}
            onCancel={onClose}
            destroyOnClose
            footer={[
                <Button key="done" type="primary" onClick={onClose}>
                    {t('tenants.users.invite.resultDone')}
                </Button>,
            ]}
        >
            <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                <Typography.Text>
                    {t('tenants.users.invite.credentialsFor', { email: result.email })}
                </Typography.Text>
                <Alert
                    type="info"
                    message={
                        <Space wrap>
                            <Typography.Text strong>{t('tenants.users.invite.passwordLabel')}</Typography.Text>
                            <Typography.Text code>{result.generatedPassword}</Typography.Text>
                            <Button size="small" icon={<CopyOutlined />} onClick={() => void copyPassword()}>
                                {t('tenants.provisioning.copyPassword')}
                            </Button>
                        </Space>
                    }
                />
                <Alert type="warning" showIcon message={t('tenants.users.invite.passwordOnceWarning')} />
                <Typography.Text strong>{t('tenants.users.invite.nextStepsTitle')}</Typography.Text>
                <Typography.Paragraph style={{ marginBottom: 0 }}>
                    <ol style={{ paddingLeft: 20, margin: 0 }}>
                        <li>{t('tenants.users.invite.nextStep1')}</li>
                        <li>
                            {portalUrl
                                ? t('tenants.users.invite.nextStep2', { portalUrl })
                                : t('tenants.users.invite.nextStep2NoUrl')}
                        </li>
                        <li>{t('tenants.users.invite.nextStep3')}</li>
                    </ol>
                </Typography.Paragraph>
            </Space>
        </Modal>
    );
}
