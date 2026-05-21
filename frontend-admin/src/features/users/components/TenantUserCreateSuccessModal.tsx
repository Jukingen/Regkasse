'use client';

import { Alert, Button, Modal, Space, Typography, message } from 'antd';
import { CopyOutlined } from '@ant-design/icons';

import type { TenantUserInviteResult } from '@/features/super-admin/api/tenantUsers';
import { useI18n } from '@/i18n';

export type TenantUserCreateSuccessModalProps = {
    open: boolean;
    result: TenantUserInviteResult | null;
    onClose: () => void;
};

/** Success modal for legacy invite API responses (create + assign-existing). */
export function TenantUserCreateSuccessModal({ open, result, onClose }: TenantUserCreateSuccessModalProps) {
    const { t } = useI18n();

    const copyPassword = async () => {
        if (!result?.generatedPassword) return;
        try {
            await navigator.clipboard.writeText(result.generatedPassword);
            message.success(t('tenants.provisioning.copySuccess'));
        } catch {
            message.error(t('tenants.provisioning.copyFailed'));
        }
    };

    if (!result) return null;

    const portalUrl = result.tenantPortalUrl ?? '';
    const email = result.user.email;
    const title = result.userCreated
        ? t('tenants.users.invite.resultSuccessTitle')
        : t('tenants.users.invite.resultAssignedTitle');

    return (
        <Modal
            title={title}
            open={open}
            onCancel={onClose}
            destroyOnClose
            footer={[
                <Button key="done" type="primary" onClick={onClose}>
                    {t('tenants.users.invite.resultDone')}
                </Button>,
            ]}
        >
            {result.userCreated && result.generatedPassword ? (
                <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    <Typography.Text>
                        {t('tenants.users.invite.credentialsFor', { email })}
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
            ) : (
                <Space direction="vertical" style={{ width: '100%' }}>
                    <Typography.Text>{t('tenants.users.invite.resultExisting')}</Typography.Text>
                    <Typography.Text type="secondary">{email}</Typography.Text>
                </Space>
            )}
        </Modal>
    );
}
