'use client';

/**
 * Post-create success: one-time credentials, first steps, and primary actions.
 */
import React, { useCallback, useMemo, useState } from 'react';
import { Alert, Button, Input, Modal, Space, Typography, message } from 'antd';
import { CheckCircleFilled, CopyOutlined, EyeInvisibleOutlined, EyeOutlined, LoginOutlined, PlusOutlined } from '@ant-design/icons';

import { useI18n } from '@/i18n';
import {
    buildTenantPortalUrl,
    formatTenantProvisioningHandoff,
    type TenantProvisioning,
} from '@/features/super-admin/api/adminTenants';
import { CopyIconButton } from '@/features/super-admin/components/CopyIconButton';
import styles from '@/styles/tenant-form.module.css';

export type TenantOnboardingSuccessState = {
    tenantId: string;
    tenantName: string;
    slug: string;
    contactEmail: string;
    provisioning: TenantProvisioning | null;
};

export type OnboardingSuccessModalProps = {
    success: TenantOnboardingSuccessState | null;
    onClose: () => void;
    onCreateAnother?: () => void;
    onSwitchToTenant?: (tenantId: string) => void;
    switchToTenantLoading?: boolean;
};

export function OnboardingSuccessModal({
    success,
    onClose,
    onCreateAnother,
    onSwitchToTenant,
    switchToTenantLoading = false,
}: OnboardingSuccessModalProps) {
    const { t } = useI18n();
    const [passwordVisible, setPasswordVisible] = useState(false);

    const portalUrl = useMemo(() => (success ? buildTenantPortalUrl(success.slug) : ''), [success]);

    const handoffText = useMemo(() => {
        if (!success?.provisioning) {
            return '';
        }
        return formatTenantProvisioningHandoff(
            success.tenantName,
            success.slug,
            success.provisioning,
            success.contactEmail,
        );
    }, [success]);

    const handleClose = useCallback(() => {
        setPasswordVisible(false);
        onClose();
    }, [onClose]);

    const handleCreateAnother = useCallback(() => {
        setPasswordVisible(false);
        onClose();
        onCreateAnother?.();
    }, [onClose, onCreateAnother]);

    const handleSwitchToTenant = useCallback(() => {
        if (!success?.tenantId) {
            return;
        }
        onSwitchToTenant?.(success.tenantId);
    }, [success?.tenantId, onSwitchToTenant]);

    const copyHandoff = useCallback(async () => {
        if (!handoffText) {
            return;
        }
        try {
            await navigator.clipboard.writeText(handoffText);
            message.success(t('tenants.provisioning.copySuccess'));
        } catch {
            message.error(t('tenants.provisioning.copyFailed'));
        }
    }, [handoffText, t]);

    const notifyEmail = success?.contactEmail?.trim() || success?.provisioning?.adminEmail;
    const welcomeEmailSent = success?.provisioning?.welcomeEmailSent === true;

    return (
        <Modal
            title={null}
            open={!!success}
            onCancel={handleClose}
            width={640}
            destroyOnClose
            footer={
                success
                    ? [
                          <Button key="another" icon={<PlusOutlined />} onClick={handleCreateAnother}>
                              {t('tenants.provisioning.createAnother')}
                          </Button>,
                          <Button
                              key="switch"
                              type="primary"
                              icon={<LoginOutlined />}
                              loading={switchToTenantLoading}
                              disabled={!success.tenantId || !onSwitchToTenant}
                              onClick={handleSwitchToTenant}
                          >
                              {t('tenants.provisioning.switchToTenant')}
                          </Button>,
                      ]
                    : null
            }
        >
            {success ? (
                <Space direction="vertical" size="large" style={{ width: '100%' }}>
                    <Alert
                        type="success"
                        showIcon
                        icon={<CheckCircleFilled />}
                        message={t('tenants.provisioning.successHeadline', { name: success.tenantName })}
                    />

                    {!success.provisioning ? (
                        <Alert type="warning" showIcon message={t('tenants.provisioning.missingWarning')} />
                    ) : (
                        <>
                            <div>
                                <Typography.Title level={5} className={styles.successSectionTitle}>
                                    {t('tenants.provisioning.credentialsTitle')}
                                </Typography.Title>
                                <div className={styles.successPanel}>
                                    <div className={styles.successCredentialRow}>
                                        <Typography.Text strong>
                                            {t('tenants.provisioning.adminEmailLabel')}:
                                        </Typography.Text>
                                        <Typography.Text>{success.provisioning.adminEmail}</Typography.Text>
                                        <CopyIconButton
                                            text={success.provisioning.adminEmail}
                                            ariaLabel={t('tenants.provisioning.copyEmail')}
                                        />
                                    </div>
                                    <div className={styles.successCredentialRow}>
                                        <Typography.Text strong>
                                            {t('tenants.provisioning.password')}:
                                        </Typography.Text>
                                        <Space.Compact style={{ flex: 1, minWidth: 0 }}>
                                            <Input
                                                readOnly
                                                size="small"
                                                type={passwordVisible ? 'text' : 'password'}
                                                value={success.provisioning.generatedPassword}
                                                aria-label={t('tenants.provisioning.password')}
                                            />
                                            <Button
                                                size="small"
                                                icon={passwordVisible ? <EyeInvisibleOutlined /> : <EyeOutlined />}
                                                onClick={() => setPasswordVisible((v) => !v)}
                                                aria-label={
                                                    passwordVisible
                                                        ? t('tenants.provisioning.hidePassword')
                                                        : t('tenants.provisioning.showPassword')
                                                }
                                            />
                                            <CopyIconButton
                                                text={success.provisioning.generatedPassword}
                                                ariaLabel={t('tenants.provisioning.copyPassword')}
                                            />
                                        </Space.Compact>
                                    </div>
                                </div>
                                <Alert
                                    type="warning"
                                    showIcon
                                    style={{ marginTop: 12 }}
                                    message={t('tenants.provisioning.passwordWarning')}
                                />
                                {success.provisioning.forcePasswordChangeOnNextLogin ? (
                                    <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                                        {t('tenants.provisioning.forcePasswordChange')}
                                    </Typography.Paragraph>
                                ) : null}
                            </div>

                            <div>
                                <Typography.Title level={5} className={styles.successSectionTitle}>
                                    {t('tenants.provisioning.firstStepsTitle')}
                                </Typography.Title>
                                <ol className={styles.successStepsList}>
                                    <li>
                                        {t('tenants.provisioning.firstStepLogin')}{' '}
                                        <Typography.Link
                                            href={portalUrl}
                                            target="_blank"
                                            rel="noopener noreferrer"
                                            copyable={{ text: portalUrl }}
                                        >
                                            {portalUrl}
                                        </Typography.Link>
                                    </li>
                                    <li>{t('tenants.provisioning.firstStepChangePassword')}</li>
                                    <li>{t('tenants.provisioning.firstStepProducts')}</li>
                                    <li>{t('tenants.provisioning.firstStepPrinter')}</li>
                                    <li>{t('tenants.provisioning.firstStepTestSale')}</li>
                                </ol>
                            </div>

                            {notifyEmail ? (
                                <Typography.Paragraph className={styles.successEmailNote} style={{ marginBottom: 0 }}>
                                    {welcomeEmailSent
                                        ? t('tenants.provisioning.emailSent', { email: notifyEmail })
                                        : t('tenants.provisioning.emailNotSent', { email: notifyEmail })}
                                </Typography.Paragraph>
                            ) : null}

                            <Button type="link" icon={<CopyOutlined />} onClick={() => void copyHandoff()} style={{ padding: 0 }}>
                                {t('tenants.provisioning.copyAll')}
                            </Button>
                        </>
                    )}
                </Space>
            ) : null}
        </Modal>
    );
}
