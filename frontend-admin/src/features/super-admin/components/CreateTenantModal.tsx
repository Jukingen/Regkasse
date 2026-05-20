'use client';

/**
 * Super-admin: create tenant form and one-time provisioning success reveal.
 */
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert, Button, Form, Input, Modal, Space, Typography, message } from 'antd';
import { CopyOutlined, EyeInvisibleOutlined, EyeOutlined } from '@ant-design/icons';
import { useMutation } from '@tanstack/react-query';

import { useI18n } from '@/i18n';
import {
    buildTenantPortalUrl,
    createAdminTenant,
    formatTenantProvisioningHandoff,
    type AdminTenantDetail,
    type CreateAdminTenantRequest,
    type TenantProvisioning,
} from '@/features/super-admin/api/adminTenants';

export type CreateTenantFormValues = {
    name: string;
    slug: string;
    email?: string;
    phone?: string;
    address?: string;
    adminEmail?: string;
};

type SuccessState = {
    tenantName: string;
    slug: string;
    provisioning: TenantProvisioning;
};

export type CreateTenantModalProps = {
    open: boolean;
    onClose: () => void;
    onCreated?: (detail: AdminTenantDetail) => void;
};

export function CreateTenantModal({ open, onClose, onCreated }: CreateTenantModalProps) {
    const { t } = useI18n();
    const [form] = Form.useForm<CreateTenantFormValues>();
    const [success, setSuccess] = useState<SuccessState | null>(null);
    const [passwordVisible, setPasswordVisible] = useState(false);

    const createMutation = useMutation({
        mutationFn: (body: CreateAdminTenantRequest) => createAdminTenant(body),
        onSuccess: (created) => {
            form.resetFields();
            onClose();
            onCreated?.(created);
            if (created.provisioning) {
                setSuccess({
                    tenantName: created.name,
                    slug: created.slug,
                    provisioning: created.provisioning,
                });
            } else {
                message.success(t('tenants.messages.created'));
            }
        },
        onError: () => message.error(t('tenants.messages.saveFailed')),
    });

    const handleCloseSuccess = useCallback(() => {
        setSuccess(null);
        setPasswordVisible(false);
    }, []);

    useEffect(() => {
        if (!open) {
            form.resetFields();
        }
    }, [open, form]);

    const portalUrl = useMemo(
        () => (success ? buildTenantPortalUrl(success.slug) : ''),
        [success],
    );

    const handoffText = useMemo(
        () =>
            success
                ? formatTenantProvisioningHandoff(success.tenantName, success.slug, success.provisioning)
                : '',
        [success],
    );

    const copyHandoff = useCallback(async () => {
        if (!handoffText) return;
        try {
            await navigator.clipboard.writeText(handoffText);
            message.success(t('tenants.provisioning.copySuccess'));
        } catch {
            message.error(t('tenants.provisioning.copyFailed'));
        }
    }, [handoffText, t]);

    const trialDaysLabel = success?.provisioning.trialLicenseValidUntilUtc
        ? t('tenants.provisioning.trialLicense')
        : null;

    return (
        <>
            <Modal
                title={t('tenants.create.title')}
                open={open && !success}
                onCancel={onClose}
                onOk={() => form.submit()}
                confirmLoading={createMutation.isPending}
                destroyOnClose
                maskClosable={!createMutation.isPending}
            >
                <Form
                    form={form}
                    layout="vertical"
                    onFinish={(values) =>
                        createMutation.mutate({
                            name: values.name,
                            slug: values.slug,
                            email: values.email,
                            phone: values.phone,
                            address: values.address,
                            adminEmail: values.adminEmail,
                            grantTrialLicense: true,
                        })
                    }
                >
                    <Form.Item name="name" label={t('tenants.fields.name')} rules={[{ required: true }]}>
                        <Input />
                    </Form.Item>
                    <Form.Item name="slug" label={t('tenants.fields.slug')} rules={[{ required: true }]}>
                        <Input placeholder="cafe-example" />
                    </Form.Item>
                    <Form.Item name="email" label={t('tenants.fields.email')}>
                        <Input type="email" />
                    </Form.Item>
                    <Form.Item name="phone" label={t('tenants.fields.phone')}>
                        <Input />
                    </Form.Item>
                    <Form.Item name="address" label={t('tenants.fields.address')}>
                        <Input.TextArea rows={2} />
                    </Form.Item>
                    <Form.Item
                        name="adminEmail"
                        label={t('tenants.fields.adminEmail')}
                        tooltip={t('tenants.fields.adminEmailHint')}
                    >
                        <Input type="email" placeholder="admin@slug.regkasse.at" />
                    </Form.Item>
                </Form>
            </Modal>

            <Modal
                title={t('tenants.provisioning.successTitle', { name: success?.tenantName ?? '' })}
                open={!!success}
                onCancel={handleCloseSuccess}
                width={560}
                destroyOnClose
                footer={[
                    <Button key="copy" icon={<CopyOutlined />} onClick={() => void copyHandoff()}>
                        {t('tenants.provisioning.copyAll')}
                    </Button>,
                    <Button key="close" type="primary" onClick={handleCloseSuccess}>
                        {t('common.buttons.close')}
                    </Button>,
                ]}
            >
                {success ? (
                    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                        <Alert type="success" showIcon message={t('tenants.provisioning.successHeadline', { name: success.tenantName })} />
                        <Alert type="warning" showIcon message={t('tenants.provisioning.passwordWarning')} />

                        <div>
                            <Typography.Title level={5} style={{ marginTop: 0 }}>
                                {t('tenants.provisioning.credentialsTitle')}
                            </Typography.Title>
                            <Typography.Paragraph style={{ marginBottom: 8 }}>
                                <Typography.Text strong>{t('tenants.provisioning.adminEmail')}: </Typography.Text>
                                <Typography.Text copyable={{ text: success.provisioning.adminEmail }}>
                                    {success.provisioning.adminEmail}
                                </Typography.Text>
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
                                <Button
                                    icon={<CopyOutlined />}
                                    onClick={() => {
                                        void navigator.clipboard.writeText(success.provisioning.generatedPassword);
                                        message.success(t('tenants.provisioning.copySuccess'));
                                    }}
                                />
                            </Space.Compact>
                        </div>

                        {trialDaysLabel ? (
                            <Typography.Paragraph style={{ marginBottom: 0 }}>
                                {trialDaysLabel}
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

                        <div>
                            <Typography.Title level={5}>{t('tenants.provisioning.nextStepsTitle')}</Typography.Title>
                            <Typography.Paragraph style={{ marginBottom: 0 }}>
                                1. {t('tenants.provisioning.nextStepLogin')}
                            </Typography.Paragraph>
                            <Typography.Paragraph style={{ marginBottom: 0 }}>
                                2.{' '}
                                {t('tenants.provisioning.nextStepPortal')}{' '}
                                <Typography.Link href={portalUrl} target="_blank" rel="noopener noreferrer" copyable={{ text: portalUrl }}>
                                    {portalUrl}
                                </Typography.Link>
                            </Typography.Paragraph>
                            <Typography.Paragraph style={{ marginBottom: 0 }}>
                                3. {t('tenants.provisioning.nextStepCustomize')}
                            </Typography.Paragraph>
                        </div>
                    </Space>
                ) : null}
            </Modal>
        </>
    );
}
