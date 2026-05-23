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
            title={t('users.create.success')}
            open={open}
            onCancel={onClose}
            destroyOnClose
            footer={[
                <Button key="done" type="primary" onClick={onClose}>
                    {t('common.ok', { defaultValue: 'OK' })}
                </Button>,
            ]}
        >
            <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                <Typography.Text>{result.email}</Typography.Text>
                <Alert
                    type="info"
                    message={
                        <Space wrap>
                            <Typography.Text strong>{t('users.create.password')}</Typography.Text>
                            <Typography.Text code>{result.generatedPassword}</Typography.Text>
                            <Button size="small" icon={<CopyOutlined />} onClick={() => void copyPassword()}>
                                {t('users.create.copyPassword')}
                            </Button>
                        </Space>
                    }
                />
                <Alert type="warning" showIcon message={t('users.create.generatedPasswordInfo')} />
                {portalUrl ? (
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {portalUrl}
                    </Typography.Text>
                ) : null}
            </Space>
        </Modal>
    );
}
