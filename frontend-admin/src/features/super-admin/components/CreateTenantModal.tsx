'use client';

/**
 * Super-admin: create tenant with live slug validation, URL preview, and provisioning success reveal.
 */
import React, { useEffect, useState } from 'react';
import { Alert, Button, Form, Modal, Typography } from 'antd';
import { useMutation } from '@tanstack/react-query';

import { useI18n } from '@/i18n';
import { extractRawApiErrorMessage } from '@/shared/errors/extractRawApiErrorMessage';
import {
    createAdminTenant,
    type AdminTenantDetail,
    type CreateAdminTenantRequest,
} from '@/features/super-admin/api/adminTenants';
import {
    CreateTenantSuccessModal,
    type CreateTenantSuccessState,
} from '@/features/super-admin/components/CreateTenantSuccessModal';
import { TenantFormFields } from '@/features/super-admin/components/TenantFormFields';
import { normalizeTenantSlugInput } from '@/features/super-admin/lib/tenantSlug';

export type CreateTenantFormValues = {
    name: string;
    slug: string;
    email: string;
    phone?: string;
    address?: string;
    grantTrialLicense: boolean;
};

export type CreateTenantModalProps = {
    open: boolean;
    onClose: () => void;
    onCreated?: (detail: AdminTenantDetail) => void;
};

function mapApiErrorToFormFields(
    form: ReturnType<typeof Form.useForm<CreateTenantFormValues>>[0],
    message: string | undefined,
    fallback: string,
): void {
    const text = message ?? fallback;
    const lower = text.toLowerCase();
    if (lower.includes('slug')) {
        form.setFields([{ name: 'slug', errors: [text] }]);
        return;
    }
    if (lower.includes('email')) {
        form.setFields([{ name: 'email', errors: [text] }]);
        return;
    }
    form.setFields([{ name: 'formError', errors: [text] }]);
}

export function CreateTenantModal({ open, onClose, onCreated }: CreateTenantModalProps) {
    const { t } = useI18n();
    const [form] = Form.useForm<CreateTenantFormValues & { formError?: string }>();
    const [success, setSuccess] = useState<CreateTenantSuccessState | null>(null);

    const createMutation = useMutation({
        mutationFn: (body: CreateAdminTenantRequest) => createAdminTenant(body),
        onSuccess: (created) => {
            form.resetFields();
            onClose();
            onCreated?.(created);
            setSuccess({
                tenantName: created.name,
                slug: created.slug,
                provisioning: created.provisioning ?? null,
            });
        },
        onError: (error) => {
            mapApiErrorToFormFields(form, extractRawApiErrorMessage(error), t('tenants.messages.saveFailed'));
        },
    });

    useEffect(() => {
        if (!open) {
            form.resetFields();
        } else {
            form.setFieldsValue({ grantTrialLicense: true });
        }
    }, [open, form]);

    return (
        <>
            <Modal
                title={t('tenants.create.title')}
                open={open && !success}
                onCancel={onClose}
                width={640}
                destroyOnClose
                maskClosable={!createMutation.isPending}
                footer={[
                    <Button key="cancel" onClick={onClose} disabled={createMutation.isPending}>
                        {t('common.buttons.cancel')}
                    </Button>,
                    <Button
                        key="submit"
                        type="primary"
                        loading={createMutation.isPending}
                        onClick={() => form.submit()}
                    >
                        {t('tenants.create.submit')}
                    </Button>,
                ]}
            >
                <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
                    {t('tenants.create.subtitle')}
                </Typography.Paragraph>

                <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 20 }}
                    message={t('tenants.create.autoProvisionTitle')}
                    description={t('tenants.create.autoProvisionDescription')}
                />

                <Form
                    form={form}
                    layout="vertical"
                    requiredMark="optional"
                    initialValues={{ grantTrialLicense: true }}
                    onFinish={(values) => {
                        const slug = normalizeTenantSlugInput(values.slug);
                        createMutation.mutate({
                            name: values.name.trim(),
                            slug,
                            email: values.email.trim(),
                            phone: values.phone?.trim() || undefined,
                            address: values.address?.trim() || undefined,
                            grantTrialLicense: values.grantTrialLicense ?? true,
                        });
                    }}
                >
                    <TenantFormFields form={form} open={open} />
                </Form>
            </Modal>

            <CreateTenantSuccessModal success={success} onClose={() => setSuccess(null)} />
        </>
    );
}
