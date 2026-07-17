'use client';

import React, { useEffect } from 'react';
import { Alert, Button, Form, Input, Radio, Space, Tag, Typography } from 'antd';
import type { FormInstance } from 'antd';
import { CopyOutlined, ReloadOutlined } from '@ant-design/icons';

import { useAntdApp } from '@/hooks/useAntdApp';
import { usePasswordGenerator } from '@/hooks/usePasswordGenerator';
import { CreateTenantFormField } from '@/features/super-admin/components/CreateTenantFormField';
import type {
    AdminPasswordMode,
    CreateTenantWizardData,
} from '@/features/super-admin/components/CreateTenantWizard/types';
import { buildDefaultAdminEmail } from '@/features/super-admin/lib/tenantSlug';
import { validateContactEmail } from '@/features/super-admin/lib/tenantCreateValidation';
import { getTenantAppBaseDomain } from '@/lib/auth/impersonationHandoff';
import { useI18n } from '@/i18n';

export type Step2AdminUserProps = {
    form: FormInstance<CreateTenantWizardData>;
    data: CreateTenantWizardData;
    onUpdate: (patch: Partial<CreateTenantWizardData>) => void;
};

export function Step2AdminUser({ form, data, onUpdate }: Step2AdminUserProps) {
    const { t } = useI18n();
    const { message } = useAntdApp();
    const { generatePassword } = usePasswordGenerator();
    const baseDomain = getTenantAppBaseDomain();

    useEffect(() => {
        const suggested =
            data.adminEmail.trim() ||
            data.email.trim() ||
            buildDefaultAdminEmail(data.slug, baseDomain);
        form.setFieldsValue({
            adminEmail: suggested,
            adminPassword: data.adminPassword,
            passwordMode: data.passwordMode,
        });
        if (!data.adminEmail.trim() && suggested) {
            onUpdate({ adminEmail: suggested });
        }
        // Intentionally sync when entering step / slug or contact email changes.
        // eslint-disable-next-line react-hooks/exhaustive-deps -- avoid loop on onUpdate identity
    }, [form, data.slug, data.email, baseDomain]);

    const applyGeneratedPassword = (password: string) => {
        form.setFieldsValue({ adminPassword: password, passwordMode: 'auto' });
        onUpdate({ adminPassword: password, passwordMode: 'auto' });
    };

    const handleAutoPassword = () => {
        applyGeneratedPassword(generatePassword());
    };

    const handlePasswordModeChange = (mode: AdminPasswordMode) => {
        form.setFieldsValue({ passwordMode: mode });
        if (mode === 'auto') {
            if (!data.adminPassword.trim()) {
                applyGeneratedPassword(generatePassword());
            } else {
                onUpdate({ passwordMode: 'auto' });
            }
            return;
        }
        onUpdate({ passwordMode: 'manual' });
    };

    const copyGeneratedPassword = async () => {
        const password = data.adminPassword.trim();
        if (!password) {
            return;
        }
        try {
            await navigator.clipboard.writeText(password);
            message.success(t('tenants.provisioning.copySuccess'));
        } catch {
            message.error(t('tenants.provisioning.copyFailed'));
        }
    };

    return (
        <Form
            form={form}
            layout="vertical"
            requiredMark="optional"
            onValuesChange={(_, all) => {
                onUpdate({
                    adminEmail: all.adminEmail ?? '',
                    adminPassword: all.adminPassword ?? '',
                    passwordMode: all.passwordMode ?? 'auto',
                });
            }}
        >
            <CreateTenantFormField
                name="adminEmail"
                label={t('tenants.create.wizard.fields.adminEmail')}
                tooltip={t('tenants.create.wizard.fields.adminEmailTooltip')}
                hint={t('tenants.create.wizard.fields.adminEmailHint')}
                required
                rules={[
                    { required: true, message: t('tenants.create.fields.contactEmail.errors.required') },
                    {
                        validator: async (_: unknown, value: string | undefined) => {
                            const code = validateContactEmail(value);
                            if (code) {
                                throw new Error(t(`tenants.create.fields.contactEmail.errors.${code}`));
                            }
                        },
                    },
                ]}
            >
                <Input type="email" autoComplete="off" />
            </CreateTenantFormField>

            <Form.Item label={t('tenants.create.wizard.fields.role')}>
                <Tag color="blue">{t('tenants.create.wizard.fields.roleValue')}</Tag>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, marginTop: 8 }}>
                    {t('tenants.create.wizard.fields.roleHint')}
                </Typography.Paragraph>
            </Form.Item>

            <Form.Item
                name="passwordMode"
                label={t('tenants.create.wizard.fields.passwordMode')}
                rules={[{ required: true }]}
            >
                <Radio.Group
                    onChange={(event) => handlePasswordModeChange(event.target.value as AdminPasswordMode)}
                    options={[
                        { value: 'auto', label: t('tenants.create.wizard.fields.passwordAuto') },
                        { value: 'manual', label: t('tenants.create.wizard.fields.passwordManual') },
                    ]}
                />
            </Form.Item>

            <Form.Item
                shouldUpdate={(prev, next) =>
                    prev.passwordMode !== next.passwordMode || prev.adminPassword !== next.adminPassword
                }
                noStyle
            >
                {() => {
                    const mode = form.getFieldValue('passwordMode') as AdminPasswordMode;
                    const generatedPassword = (form.getFieldValue('adminPassword') as string | undefined)?.trim() ?? '';

                    if (mode === 'auto') {
                        return (
                            <Space orientation="vertical" style={{ width: '100%' }} size="middle">
                                <Button icon={<ReloadOutlined />} onClick={handleAutoPassword}>
                                    {t('tenants.create.wizard.fields.generatePassword')}
                                </Button>
                                {generatedPassword ? (
                                    <Alert
                                        type="warning"
                                        showIcon
                                        title={t('tenants.create.wizard.fields.passwordCopyWarning')}
                                        description={
                                            <Space wrap align="center">
                                                <Typography.Text code copyable={{ text: generatedPassword }}>
                                                    {generatedPassword}
                                                </Typography.Text>
                                                <Button
                                                    size="small"
                                                    icon={<CopyOutlined />}
                                                    onClick={() => void copyGeneratedPassword()}
                                                >
                                                    {t('tenants.provisioning.copyPassword')}
                                                </Button>
                                            </Space>
                                        }
                                    />
                                ) : (
                                    <Alert
                                        type="info"
                                        showIcon
                                        title={t('tenants.create.wizard.fields.passwordAutoHint')}
                                    />
                                )}
                                <Form.Item name="adminPassword" hidden>
                                    <Input />
                                </Form.Item>
                            </Space>
                        );
                    }

                    return (
                        <CreateTenantFormField
                            name="adminPassword"
                            label={t('tenants.create.wizard.fields.adminPassword')}
                            required
                            rules={[
                                { required: true, message: t('tenants.create.wizard.fields.passwordRequired') },
                                { min: 8, message: t('tenants.create.wizard.fields.passwordMin') },
                            ]}
                        >
                            <Input.Password autoComplete="new-password" />
                        </CreateTenantFormField>
                    );
                }}
            </Form.Item>
        </Form>
    );
}
