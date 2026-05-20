'use client';

/**
 * Post-create provisioning reveal: admin email, generated password with strength, copy actions.
 */
import React, { useCallback, useMemo, useState } from 'react';
import { Alert, Button, Input, Modal, Progress, Space, Typography, message } from 'antd';
import { CopyOutlined, EyeInvisibleOutlined, EyeOutlined } from '@ant-design/icons';

import { useI18n } from '@/i18n';
import {
    buildTenantPortalUrl,
    formatTenantProvisioningHandoff,
    type TenantProvisioning,
} from '@/features/super-admin/api/adminTenants';
import { CopyIconButton } from '@/features/super-admin/components/CopyIconButton';
import { evaluatePasswordStrength } from '@/features/super-admin/lib/passwordStrength';

export type CreateTenantSuccessState = {
    tenantName: string;
    slug: string;
    provisioning: TenantProvisioning | null;
};

export type CreateTenantSuccessModalProps = {
    success: CreateTenantSuccessState | null;
    onClose: () => void;
};

export function CreateTenantSuccessModal({ success, onClose }: CreateTenantSuccessModalProps) {
    const { t } = useI18n();
    const [passwordVisible, setPasswordVisible] = useState(false);

    const portalUrl = useMemo(() => (success ? buildTenantPortalUrl(success.slug) : ''), [success]);

    const handoffText = useMemo(() => {
        if (!success?.provisioning) {
            return '';
        }
        return formatTenantProvisioningHandoff(success.tenantName, success.slug, success.provisioning);
    }, [success]);

    const passwordStrength = useMemo(
        () =>
            success?.provisioning ? evaluatePasswordStrength(success.provisioning.generatedPassword) : null,
        [success],
    );

    const handleClose = useCallback(() => {
        setPasswordVisible(false);
        onClose();
    }, [onClose]);

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

    return (
        <Modal
            title={t('tenants.provisioning.successTitle')}
            open={!!success}
            onCancel={handleClose}
            width={600}
            destroyOnClose
            footer={[
                ...(success?.provisioning
                    ? [
                          <Button key="copy" icon={<CopyOutlined />} onClick={() => void copyHandoff()}>
                              {t('tenants.provisioning.copyAll')}
                          </Button>,
                      ]
                    : []),
                <Button key="close" type="primary" onClick={handleClose}>
                    {t('common.buttons.close')}
                </Button>,
            ]}
        >
            {success ? (
                <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    <Alert
                        type="success"
                        showIcon
                        message={t('tenants.provisioning.successHeadline', { name: success.tenantName })}
                    />

                    {!success.provisioning ? (
                        <Alert type="warning" showIcon message={t('tenants.provisioning.missingWarning')} />
                    ) : (
                        <>
                            <Alert type="warning" showIcon message={t('tenants.provisioning.passwordWarning')} />

                            <div>
                                <Typography.Title level={5} style={{ marginTop: 0 }}>
                                    {t('tenants.provisioning.credentialsTitle')}
                                </Typography.Title>
                                <Typography.Paragraph style={{ marginBottom: 8 }}>
                                    <Typography.Text strong>{t('tenants.provisioning.loginEmail')}: </Typography.Text>
                                    <Typography.Text>{success.provisioning.adminEmail}</Typography.Text>
                                    <CopyIconButton
                                        text={success.provisioning.adminEmail}
                                        ariaLabel={t('tenants.create.fields.adminEmail.copy')}
                                    />
                                </Typography.Paragraph>
                                <Typography.Paragraph style={{ marginBottom: 4 }}>
                                    <Typography.Text strong>{t('tenants.provisioning.password')}: </Typography.Text>
                                    <Typography.Text type="secondary">
                                        {t('tenants.provisioning.passwordOnce')}
                                    </Typography.Text>
                                </Typography.Paragraph>
                                <Space.Compact style={{ width: '100%' }}>
                                    <Input
                                        readOnly
                                        type={passwordVisible ? 'text' : 'password'}
                                        value={success.provisioning.generatedPassword}
                                        aria-label={t('tenants.provisioning.password')}
                                    />
                                    <Button
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
                                {passwordStrength ? (
                                    <div style={{ marginTop: 12 }}>
                                        <Typography.Text type="secondary">
                                            {t('tenants.provisioning.passwordStrength.label')}:{' '}
                                            {t(passwordStrength.labelKey)}
                                        </Typography.Text>
                                        <Progress
                                            percent={passwordStrength.percent}
                                            showInfo={false}
                                            strokeColor={
                                                passwordStrength.level >= 4
                                                    ? '#52c41a'
                                                    : passwordStrength.level >= 3
                                                      ? '#73d13d'
                                                      : passwordStrength.level >= 2
                                                        ? '#faad14'
                                                        : '#ff4d4f'
                                            }
                                            size="small"
                                        />
                                    </div>
                                ) : null}
                            </div>

                            {success.provisioning.trialLicenseValidUntilUtc ? (
                                <Typography.Paragraph style={{ marginBottom: 0 }}>
                                    {t('tenants.provisioning.trialLicense')}
                                </Typography.Paragraph>
                            ) : null}

                            <Typography.Paragraph style={{ marginBottom: 0 }}>
                                {t('tenants.provisioning.register', {
                                    location: 'Hauptkasse',
                                    number: success.provisioning.cashRegisterNumber,
                                })}
                            </Typography.Paragraph>

                            <Typography.Paragraph style={{ marginBottom: 0 }}>
                                {t('tenants.provisioning.products', {
                                    count: success.provisioning.productIds.length,
                                })}
                            </Typography.Paragraph>
                        </>
                    )}

                    <div>
                        <Typography.Title level={5}>{t('tenants.provisioning.nextStepsTitle')}</Typography.Title>
                        <Typography.Paragraph style={{ marginBottom: 0 }}>
                            1.{' '}
                            {success.provisioning
                                ? t('tenants.provisioning.nextStepLogin')
                                : t('tenants.provisioning.nextStepPortalOnly')}
                        </Typography.Paragraph>
                        <Typography.Paragraph style={{ marginBottom: 0 }}>
                            2. {t('tenants.provisioning.nextStepPortal')}{' '}
                            <Typography.Link
                                href={portalUrl}
                                target="_blank"
                                rel="noopener noreferrer"
                                copyable={{ text: portalUrl }}
                            >
                                {portalUrl}
                            </Typography.Link>
                        </Typography.Paragraph>
                        {success.provisioning ? (
                            <Typography.Paragraph style={{ marginBottom: 0 }}>
                                3. {t('tenants.provisioning.nextStepCustomize')}
                            </Typography.Paragraph>
                        ) : null}
                    </div>
                </Space>
            ) : null}
        </Modal>
    );
}
