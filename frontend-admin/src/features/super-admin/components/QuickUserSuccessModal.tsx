'use client';

import { Alert, Button, Descriptions, Modal, Space, Typography, message } from 'antd';
import { CheckCircleOutlined, CopyOutlined } from '@ant-design/icons';

import type { CreateQuickUserResult } from '@/features/super-admin/api/quickUser';
import { useSuperAdminPlatformPolicy } from '@/features/super-admin/auth/superAdminPlatformPolicy';
import { useI18n } from '@/i18n';

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
    const { t } = useI18n();
    const { canProvisionTenantCredentials } = useSuperAdminPlatformPolicy();

    if (!canProvisionTenantCredentials || !result?.success) {
        return null;
    }

    const portalUrl = result.tenantPortalUrl ?? `https://${tenantSlug}.regkasse.at`;
    const displayRole = result.role ?? role;
    const roleLabelKey = `users.create.roleOptions.${displayRole}.label` as const;
    const roleLabel = t(roleLabelKey) !== roleLabelKey ? t(roleLabelKey) : displayRole;

    const copyPassword = async () => {
        if (!result.generatedPassword) return;
        try {
            await navigator.clipboard.writeText(result.generatedPassword);
            message.success(t('tenants.provisioning.copySuccess'));
        } catch {
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
            destroyOnClose
            footer={[
                <Button key="another" onClick={onGenerateAnother}>
                    {t('tenants.users.quick.result.generateAnother')}
                </Button>,
                <Button key="done" type="primary" onClick={onClose}>
                    {t('tenants.users.quick.result.done')}
                </Button>,
            ]}
            width={520}
        >
            <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                <Typography.Text>
                    <Typography.Text strong>{t('tenants.users.quick.result.userLabel')} </Typography.Text>
                    {result.email}
                </Typography.Text>

                <Alert
                    type="info"
                    message={
                        <Space wrap align="center">
                            <Typography.Text strong>{t('tenants.users.quick.result.passwordLabel')}</Typography.Text>
                            <Typography.Text code style={{ fontSize: 14 }}>
                                {result.generatedPassword}
                            </Typography.Text>
                            <Button size="small" icon={<CopyOutlined />} onClick={() => void copyPassword()}>
                                {t('tenants.provisioning.copyPassword')}
                            </Button>
                        </Space>
                    }
                />

                <Alert type="warning" showIcon message={t('tenants.users.quick.result.passwordOnceWarning')} />

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
