'use client';

/**
 * Super-admin tenant onboarding wizard: form → progress → success or structured error.
 */
import React, { useCallback, useEffect, useState } from 'react';
import { Button, Form, Modal } from 'antd';
import { useMutation } from '@tanstack/react-query';

import { useI18n } from '@/i18n';
import {
    createAdminTenant,
    type AdminTenantDetail,
    type CreateAdminTenantRequest,
} from '@/features/super-admin/api/adminTenants';
import { CreateTenantProcessingView } from '@/features/super-admin/components/CreateTenantProcessingView';
import { OnboardingErrorModal } from '@/features/super-admin/components/OnboardingErrorModal';
import {
    OnboardingSuccessModal,
    type TenantOnboardingSuccessState,
} from '@/features/super-admin/components/OnboardingSuccessModal';
import { TenantFormFields } from '@/features/super-admin/components/TenantFormFields';
import { useTenantCreateFormFields } from '@/features/super-admin/hooks/useTenantCreateFormFields';
import { useTenantOnboardingProgress } from '@/features/super-admin/hooks/useTenantOnboardingProgress';
import { parseTenantOnboardingError } from '@/features/super-admin/lib/parseTenantOnboardingError';
import { getTenantAppBaseDomain } from '@/lib/auth/impersonationHandoff';
import { normalizeTenantSlugInput } from '@/features/super-admin/lib/tenantSlug';
import type { TenantOnboardingError } from '@/features/super-admin/lib/parseTenantOnboardingError';

export type CreateTenantFormValues = {
    name: string;
    slug: string;
    email: string;
    phone?: string;
    address?: string;
    grantTrialLicense: boolean;
    autoDemoSetup: boolean;
    importDemoProducts: boolean;
};

export type CreateTenantWizardProps = {
    open: boolean;
    onClose: () => void;
    onCreated?: (detail: AdminTenantDetail) => void;
    onCreateAnother?: () => void;
    onSwitchToTenant?: (tenantId: string) => void;
    switchToTenantLoading?: boolean;
};

type WizardPhase = 'form' | 'processing' | 'processingDone';

export function CreateTenantWizard(props: CreateTenantWizardProps) {
    if (!props.open) {
        return null;
    }
    return <CreateTenantWizardContent {...props} />;
}

function CreateTenantWizardContent({
    open,
    onClose,
    onCreated,
    onCreateAnother,
    onSwitchToTenant,
    switchToTenantLoading,
}: CreateTenantWizardProps) {
    const { t } = useI18n();
    const baseDomain = getTenantAppBaseDomain();
    const [form] = Form.useForm<CreateTenantFormValues & { formError?: string }>();
    const [success, setSuccess] = useState<TenantOnboardingSuccessState | null>(null);
    const [phase, setPhase] = useState<WizardPhase>('form');
    const [onboardingError, setOnboardingError] = useState<TenantOnboardingError | null>(null);
    const [errorContext, setErrorContext] = useState<{ companyName: string; slug: string } | null>(null);
    const [processingContext, setProcessingContext] = useState<{
        name: string;
        slug: string;
        contactEmail: string;
        grantTrialLicense: boolean;
    } | null>(null);

    const formFields = useTenantCreateFormFields(form, open);
    const { canSubmit } = formFields;

    const grantTrialLicense = processingContext?.grantTrialLicense ?? true;
    const progressPhase =
        phase === 'processingDone' ? 'success' : phase === 'processing' ? 'running' : 'idle';
    const { definitions, statuses } = useTenantOnboardingProgress(grantTrialLicense, progressPhase);

    const resetFlow = useCallback(() => {
        setPhase('form');
        setProcessingContext(null);
        setOnboardingError(null);
        setErrorContext(null);
    }, []);

    const createMutation = useMutation({
        mutationFn: (body: CreateAdminTenantRequest) => createAdminTenant(body),
        onSuccess: (created) => {
            const contactEmail =
                created.email?.trim() || processingContext?.contactEmail || '';
            setPhase('processingDone');
            window.setTimeout(() => {
                form.resetFields();
                resetFlow();
                onClose();
                onCreated?.(created);
                setSuccess({
                    tenantId: created.id,
                    tenantName: created.name,
                    slug: created.slug,
                    contactEmail,
                    provisioning: created.provisioning ?? null,
                });
            }, 500);
        },
        onError: (error) => {
            const parsed = parseTenantOnboardingError(error, t('tenants.messages.saveFailed'));
            setOnboardingError(parsed);
            setErrorContext({
                companyName: processingContext?.name ?? form.getFieldValue('name') ?? '',
                slug: processingContext?.slug ?? form.getFieldValue('slug') ?? '',
            });
            setPhase('form');
            setProcessingContext(null);
        },
    });

    useEffect(() => {
        if (!open) {
            form.resetFields();
            if (phase !== 'processing' && phase !== 'processingDone') {
                resetFlow();
            }
        } else if (phase === 'form') {
            form.setFieldsValue({
                grantTrialLicense: true,
                autoDemoSetup: true,
                importDemoProducts: true,
            });
        }
    }, [open, form, resetFlow, phase]);

    const isProcessing = phase === 'processing' || phase === 'processingDone' || createMutation.isPending;

    const handleWizardClose = () => {
        if (phase === 'processing') {
            return;
        }
        onClose();
    };

    const submitFromForm = (values: CreateTenantFormValues) => {
        const slug = normalizeTenantSlugInput(values.slug);
        const grantTrial = values.grantTrialLicense ?? true;
        setProcessingContext({
            name: values.name.trim(),
            slug,
            contactEmail: values.email.trim(),
            grantTrialLicense: grantTrial,
        });
        setOnboardingError(null);
        setPhase('processing');
        createMutation.mutate({
            name: values.name.trim(),
            slug,
            email: values.email.trim(),
            adminEmail: values.email.trim(),
            phone: values.phone?.trim() || undefined,
            address: values.address?.trim() || undefined,
            grantTrialLicense: grantTrial,
            importDemoMenu: values.importDemoProducts ?? true,
        });
    };

    const handleDismissError = useCallback(() => {
        setOnboardingError(null);
        setErrorContext(null);
    }, []);

    const handleErrorCancel = useCallback(() => {
        handleDismissError();
        onClose();
    }, [handleDismissError, onClose]);

    const handleErrorTrySlug = useCallback(
        (slug: string) => {
            form.setFieldsValue({ slug: normalizeTenantSlugInput(slug) });
            setOnboardingError(null);
            setErrorContext(null);
            void form.validateFields(['slug']);
        },
        [form],
    );

    const modalTitle =
        phase === 'processing' || phase === 'processingDone'
            ? t('tenants.create.processing.title')
            : t('tenants.create.title');

    return (
        <>
            <Modal
                title={modalTitle}
                open={open && !success && phase !== 'form' && isProcessing}
                onCancel={handleWizardClose}
                width={640}
                destroyOnHidden
                mask={{ closable: false }}
                closable={false}
                footer={null}
            >
                {processingContext ? (
                    <CreateTenantProcessingView
                        definitions={definitions}
                        statuses={statuses}
                        companyName={processingContext.name}
                        slug={processingContext.slug}
                        baseDomain={baseDomain}
                        phase={phase === 'processingDone' ? 'success' : 'running'}
                    />
                ) : null}
            </Modal>

            <Modal
                title={modalTitle}
                open={open && !success && phase === 'form'}
                onCancel={handleWizardClose}
                width={640}
                destroyOnHidden
                footer={[
                    <Button key="cancel" onClick={handleWizardClose}>
                        {t('common.buttons.cancel')}
                    </Button>,
                    <Button key="submit" type="primary" disabled={!canSubmit} onClick={() => form.submit()}>
                        {t('tenants.create.submit')}
                    </Button>,
                ]}
            >
                <Form
                    form={form}
                    layout="vertical"
                    requiredMark="optional"
                    initialValues={{ grantTrialLicense: true, autoDemoSetup: true, importDemoProducts: true }}
                    onFinish={submitFromForm}
                >
                    <TenantFormFields form={form} open={open} fieldState={formFields} />
                </Form>
            </Modal>

            <OnboardingErrorModal
                open={!!onboardingError}
                error={onboardingError}
                companyName={errorContext?.companyName}
                attemptedSlug={errorContext?.slug}
                onTrySlug={handleErrorTrySlug}
                onDismiss={handleDismissError}
                onCancel={handleErrorCancel}
            />

            <OnboardingSuccessModal
                success={success}
                onClose={() => setSuccess(null)}
                onCreateAnother={onCreateAnother}
                onSwitchToTenant={onSwitchToTenant}
                switchToTenantLoading={switchToTenantLoading}
            />
        </>
    );
}
