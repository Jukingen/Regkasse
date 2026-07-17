'use client';

import React, { useCallback, useMemo, useState } from 'react';
import { Alert, Button, Descriptions, Result, Space, Typography } from 'antd';
import {
    CopyOutlined,
    EyeInvisibleOutlined,
    EyeOutlined,
    LoginOutlined,
    MailOutlined,
    PlusOutlined,
} from '@ant-design/icons';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import {
    buildTenantPortalUrl,
    formatTenantProvisioningHandoff,
} from '@/features/super-admin/api/adminTenants';
import { CopyIconButton } from '@/features/super-admin/components/CopyIconButton';
import type {
    CreateTenantWizardData,
    TenantOnboardingSuccessState,
} from '@/features/super-admin/components/CreateTenantWizard/types';

export type Step5ResultProps = {
    success: TenantOnboardingSuccessState;
    /** Wizard draft used for register / license display on the result screen. */
    data?: CreateTenantWizardData;
    onClose: () => void;
    onCreateAnother?: () => void;
    onSwitchToTenant?: (tenantId: string) => void;
    switchToTenantLoading?: boolean;
};

export function Step5Result({
    success,
    data,
    onClose,
    onCreateAnother,
    onSwitchToTenant,
    switchToTenantLoading = false,
}: Step5ResultProps) {
    const { message } = useAntdApp();
    const { t } = useI18n();
    const [passwordVisible, setPasswordVisible] = useState(false);

    const portalUrl = useMemo(() => buildTenantPortalUrl(success.slug), [success.slug]);
    const adminEmail = success.provisioning?.adminEmail || data?.adminEmail || '—';
    const adminPassword = success.provisioning?.generatedPassword || data?.adminPassword || '';
    const registerName = success.provisioning?.cashRegisterNumber || data?.registerNumber || '—';
    const licenseDays = data?.licenseDays;
    const notifyEmail = success.contactEmail?.trim() || success.provisioning?.adminEmail;
    const welcomeEmailSent = success.provisioning?.welcomeEmailSent === true;

    const handoffText = useMemo(() => {
        if (success.provisioning) {
            return formatTenantProvisioningHandoff(
                success.tenantName,
                success.slug,
                success.provisioning,
                success.contactEmail,
            );
        }
        return [
            `${t('tenants.create.fields.name.label')}: ${success.tenantName}`,
            `${t('tenants.create.fields.slug.label')}: ${success.slug}`,
            `${t('tenants.create.wizard.fields.adminEmail')}: ${adminEmail}`,
            `${t('tenants.create.wizard.fields.adminPassword')}: ${adminPassword}`,
            `${t('tenants.create.wizard.summary.loginUrl')}: ${portalUrl}`,
        ].join('\n');
    }, [success, adminEmail, adminPassword, portalUrl, t]);

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
    }, [handoffText, message, t]);

    return (
        <Result
            status="success"
            title={t('tenants.provisioning.successHeadline', { name: success.tenantName })}
            subTitle={t('tenants.create.wizard.result.subtitle')}
            extra={
                <Space wrap>
                    <Button key="copy" icon={<CopyOutlined />} onClick={() => void copyHandoff()}>
                        {t('tenants.provisioning.copyAll')}
                    </Button>
                    <Button
                        key="email"
                        icon={<MailOutlined />}
                        disabled
                        title={t('tenants.create.wizard.result.sendEmailDisabled')}
                    >
                        {t('tenants.create.wizard.result.sendEmail')}
                    </Button>
                    <Button
                        key="another"
                        icon={<PlusOutlined />}
                        onClick={() => {
                            setPasswordVisible(false);
                            onCreateAnother?.();
                        }}
                    >
                        {t('tenants.provisioning.createAnother')}
                    </Button>
                    <Button
                        key="switch"
                        type="primary"
                        icon={<LoginOutlined />}
                        loading={switchToTenantLoading}
                        disabled={!success.tenantId || !onSwitchToTenant}
                        onClick={() => onSwitchToTenant?.(success.tenantId)}
                    >
                        {t('tenants.provisioning.switchToTenant')}
                    </Button>
                    <Button key="done" onClick={onClose}>
                        {t('common.buttons.close')}
                    </Button>
                </Space>
            }
        >
            {!success.provisioning ? (
                <Alert type="warning" showIcon title={t('tenants.provisioning.missingWarning')} />
            ) : (
                <>
                    <Descriptions bordered size="small" column={1} style={{ textAlign: 'left' }}>
                        <Descriptions.Item label={t('tenants.create.fields.name.label')}>
                            {success.tenantName}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('tenants.create.fields.slug.label')}>
                            {success.slug}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('tenants.create.wizard.fields.adminEmail')}>
                            <Space>
                                {adminEmail}
                                <CopyIconButton
                                    text={adminEmail}
                                    ariaLabel={t('tenants.provisioning.copyEmail')}
                                />
                            </Space>
                        </Descriptions.Item>
                        <Descriptions.Item label={t('tenants.create.wizard.fields.adminPassword')}>
                            <Space wrap>
                                <Typography.Text code>
                                    {passwordVisible ? adminPassword : '••••••••••••'}
                                </Typography.Text>
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
                                {adminPassword ? (
                                    <CopyIconButton
                                        text={adminPassword}
                                        ariaLabel={t('tenants.provisioning.copyPassword')}
                                    />
                                ) : null}
                            </Space>
                        </Descriptions.Item>
                        <Descriptions.Item label={t('tenants.create.wizard.fields.registerName')}>
                            {registerName}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('tenants.create.wizard.fields.licenseType')}>
                            {licenseDays != null
                                ? t('tenants.create.wizard.fields.licenseDaysOption', { days: licenseDays })
                                : success.provisioning.trialLicenseValidUntilUtc
                                  ? t('tenants.create.wizard.result.licenseUntil', {
                                        date: success.provisioning.trialLicenseValidUntilUtc.slice(0, 10),
                                    })
                                  : '—'}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('tenants.create.wizard.summary.loginUrl')}>
                            <Typography.Link href={portalUrl} target="_blank" rel="noopener noreferrer" copyable>
                                {portalUrl}
                            </Typography.Link>
                        </Descriptions.Item>
                    </Descriptions>

                    <Alert
                        type="warning"
                        showIcon
                        style={{ marginTop: 16, textAlign: 'left' }}
                        title={t('tenants.provisioning.passwordWarning')}
                        description={t('tenants.create.wizard.result.passwordHandoffHint')}
                    />

                    {notifyEmail ? (
                        <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0, textAlign: 'left' }}>
                            {welcomeEmailSent
                                ? t('tenants.provisioning.emailSent', { email: notifyEmail })
                                : t('tenants.provisioning.emailNotSent', { email: notifyEmail })}
                        </Typography.Paragraph>
                    ) : null}
                </>
            )}
        </Result>
    );
}
