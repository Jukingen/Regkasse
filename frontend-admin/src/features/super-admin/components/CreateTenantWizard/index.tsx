'use client';

/**
 * Super-admin multi-step tenant onboarding wizard:
 * tenant info → admin → register/license → summary → processing → result.
 */
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Button, Form, Modal, Steps } from 'antd';
import { useMutation } from '@tanstack/react-query';

import { useI18n } from '@/i18n';
import {
    createAdminTenant,
    type CreateAdminTenantRequest,
} from '@/features/super-admin/api/adminTenants';
import { CreateTenantProcessingView } from '@/features/super-admin/components/CreateTenantProcessingView';
import { OnboardingErrorModal } from '@/features/super-admin/components/OnboardingErrorModal';
import { buildCreateTenantRequest } from '@/features/super-admin/components/CreateTenantWizard/buildCreateTenantRequest';
import { Step1TenantInfo } from '@/features/super-admin/components/CreateTenantWizard/Step1TenantInfo';
import { Step2AdminUser } from '@/features/super-admin/components/CreateTenantWizard/Step2AdminUser';
import { Step3RegisterLicense } from '@/features/super-admin/components/CreateTenantWizard/Step3RegisterLicense';
import { Step4Summary } from '@/features/super-admin/components/CreateTenantWizard/Step4Summary';
import { Step5Result } from '@/features/super-admin/components/CreateTenantWizard/Step5Result';
import {
    createEmptyWizardData,
    WIZARD_STEP_KEYS,
    type CreateTenantWizardData,
    type CreateTenantWizardProps,
    type TenantOnboardingSuccessState,
} from '@/features/super-admin/components/CreateTenantWizard/types';
import { useTenantCreateFormFields } from '@/features/super-admin/hooks/useTenantCreateFormFields';
import { useTenantOnboardingProgress } from '@/features/super-admin/hooks/useTenantOnboardingProgress';
import { parseTenantOnboardingError } from '@/features/super-admin/lib/parseTenantOnboardingError';
import type { TenantOnboardingError } from '@/features/super-admin/lib/parseTenantOnboardingError';
import { getTenantAppBaseDomain } from '@/lib/auth/impersonationHandoff';
import { normalizeTenantSlugInput } from '@/features/super-admin/lib/tenantSlug';

export type {
    CreateTenantFormValues,
    CreateTenantWizardData,
    CreateTenantWizardProps,
    TenantOnboardingSuccessState,
} from '@/features/super-admin/components/CreateTenantWizard/types';

type WizardPhase = 'steps' | 'processing' | 'processingDone' | 'result';

const FORM_STEP_COUNT = 4;

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
    const [form] = Form.useForm<CreateTenantWizardData>();
    const [data, setData] = useState<CreateTenantWizardData>(createEmptyWizardData);
    const [stepIndex, setStepIndex] = useState(0);
    const [phase, setPhase] = useState<WizardPhase>('steps');
    const [success, setSuccess] = useState<TenantOnboardingSuccessState | null>(null);
    const [onboardingError, setOnboardingError] = useState<TenantOnboardingError | null>(null);
    const [errorContext, setErrorContext] = useState<{ companyName: string; slug: string } | null>(null);

    const formFields = useTenantCreateFormFields(form, open && stepIndex === 0);
    const { canSubmit: canAdvanceStep1 } = formFields;

    const progressPhase =
        phase === 'processingDone' ? 'success' : phase === 'processing' ? 'running' : 'idle';
    const { definitions, statuses } = useTenantOnboardingProgress(true, progressPhase);

    const updateData = useCallback((patch: Partial<CreateTenantWizardData>) => {
        setData((prev) => ({ ...prev, ...patch }));
    }, []);

    const resetFlow = useCallback(() => {
        const empty = createEmptyWizardData();
        setData(empty);
        form.resetFields();
        form.setFieldsValue(empty);
        setStepIndex(0);
        setPhase('steps');
        setSuccess(null);
        setOnboardingError(null);
        setErrorContext(null);
    }, [form]);

    useEffect(() => {
        if (!open) {
            if (phase !== 'processing' && phase !== 'processingDone') {
                resetFlow();
            }
        }
    }, [open, phase, resetFlow]);

    const createMutation = useMutation({
        mutationFn: (body: CreateAdminTenantRequest) => createAdminTenant(body),
        onSuccess: (created) => {
            const contactEmail = created.email?.trim() || data.email.trim();
            setPhase('processingDone');
            window.setTimeout(() => {
                onCreated?.(created);
                setSuccess({
                    tenantId: created.id,
                    tenantName: created.name,
                    slug: created.slug,
                    contactEmail,
                    provisioning: created.provisioning ?? null,
                });
                setStepIndex(4);
                setPhase('result');
            }, 500);
        },
        onError: (error) => {
            const parsed = parseTenantOnboardingError(error, t('tenants.messages.saveFailed'));
            setOnboardingError(parsed);
            setErrorContext({
                companyName: data.name,
                slug: data.slug,
            });
            setPhase('steps');
            setStepIndex(0);
        },
    });

    const isProcessing = phase === 'processing' || phase === 'processingDone';

    const handleWizardClose = () => {
        if (phase === 'processing') {
            return;
        }
        onClose();
    };

    const handleCreateAnother = () => {
        resetFlow();
        onCreateAnother?.();
    };

    const goNext = async () => {
        if (stepIndex === 0) {
            await form.validateFields(['name', 'slug', 'email', 'phone', 'address']);
            if (!canAdvanceStep1) {
                return;
            }
            const values = form.getFieldsValue();
            updateData({
                name: values.name?.trim() ?? data.name,
                slug: normalizeTenantSlugInput(values.slug ?? data.slug),
                email: values.email?.trim() ?? data.email,
                phone: values.phone,
                address: values.address,
            });
            setStepIndex(1);
            return;
        }

        if (stepIndex === 1) {
            await form.validateFields(['adminEmail', 'passwordMode']);
            const mode = form.getFieldValue('passwordMode') as CreateTenantWizardData['passwordMode'];
            if (mode === 'manual') {
                await form.validateFields(['adminPassword']);
            }
            const values = form.getFieldsValue();
            updateData({
                adminEmail: values.adminEmail?.trim() ?? data.adminEmail,
                adminPassword: values.adminPassword ?? data.adminPassword,
                passwordMode: values.passwordMode ?? data.passwordMode,
            });
            setStepIndex(2);
            return;
        }

        if (stepIndex === 2) {
            await form.validateFields([
                'registerNumber',
                'licenseDays',
                'licenseStartDate',
                'importDemoProducts',
            ]);
            const values = form.getFieldsValue();
            updateData({
                registerNumber: values.registerNumber?.trim() || data.registerNumber,
                licenseDays: values.licenseDays ?? data.licenseDays,
                licenseStartDate: values.licenseStartDate ?? data.licenseStartDate,
                importDemoProducts: values.importDemoProducts ?? data.importDemoProducts,
            });
            setStepIndex(3);
            return;
        }

        if (stepIndex === 3) {
            const requestBody = buildCreateTenantRequest(data);
            setPhase('processing');
            createMutation.mutate(requestBody);
        }
    };

    const goBack = () => {
        if (stepIndex <= 0 || isProcessing || phase === 'result') {
            return;
        }
        setStepIndex((i) => i - 1);
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
            const normalized = normalizeTenantSlugInput(slug);
            form.setFieldsValue({ slug: normalized });
            updateData({ slug: normalized });
            setOnboardingError(null);
            setErrorContext(null);
            setStepIndex(0);
            setPhase('steps');
            void form.validateFields(['slug']);
        },
        [form, updateData],
    );

    const stepItems = useMemo(
        () =>
            WIZARD_STEP_KEYS.map((key) => ({
                title: t(`tenants.create.wizard.steps.${key}`),
            })),
        [t],
    );

    const modalTitle =
        phase === 'processing' || phase === 'processingDone'
            ? t('tenants.create.processing.title')
            : phase === 'result'
              ? t('tenants.provisioning.successTitle')
              : t('tenants.create.title');

    const showStepsChrome = phase === 'steps' || phase === 'result';
    const displayStepIndex = phase === 'result' ? 4 : Math.min(stepIndex, FORM_STEP_COUNT - 1);

    const footer =
        phase === 'result' ? null : isProcessing ? null : (
            <>
                <Button key="cancel" onClick={handleWizardClose}>
                    {t('common.buttons.cancel')}
                </Button>
                {stepIndex > 0 ? (
                    <Button key="back" onClick={goBack}>
                        {t('tenants.create.wizard.back')}
                    </Button>
                ) : null}
                <Button
                    key="next"
                    type="primary"
                    disabled={stepIndex === 0 && !canAdvanceStep1}
                    loading={createMutation.isPending}
                    onClick={() => void goNext()}
                >
                    {stepIndex === 3
                        ? t('tenants.create.wizard.confirm')
                        : t('tenants.create.wizard.next')}
                </Button>
            </>
        );

    return (
        <>
            <Modal
                title={modalTitle}
                open={open}
                onCancel={handleWizardClose}
                width={720}
                destroyOnHidden
                maskClosable={phase !== 'processing'}
                closable={phase !== 'processing'}
                footer={footer}
            >
                {showStepsChrome ? (
                    <Steps
                        current={phase === 'result' ? 4 : displayStepIndex}
                        size="small"
                        style={{ marginBottom: 24 }}
                        items={stepItems}
                    />
                ) : null}

                {isProcessing ? (
                    <CreateTenantProcessingView
                        definitions={definitions}
                        statuses={statuses}
                        companyName={data.name}
                        slug={data.slug}
                        baseDomain={baseDomain}
                        phase={phase === 'processingDone' ? 'success' : 'running'}
                    />
                ) : null}

                {phase === 'steps' && stepIndex === 0 ? (
                    <Step1TenantInfo form={form} open={open} data={data} onUpdate={updateData} />
                ) : null}
                {phase === 'steps' && stepIndex === 1 ? (
                    <Step2AdminUser form={form} data={data} onUpdate={updateData} />
                ) : null}
                {phase === 'steps' && stepIndex === 2 ? (
                    <Step3RegisterLicense form={form} data={data} onUpdate={updateData} />
                ) : null}
                {phase === 'steps' && stepIndex === 3 ? <Step4Summary data={data} /> : null}
                {phase === 'result' && success ? (
                    <Step5Result
                        success={success}
                        data={data}
                        onClose={handleWizardClose}
                        onCreateAnother={handleCreateAnother}
                        onSwitchToTenant={onSwitchToTenant}
                        switchToTenantLoading={switchToTenantLoading}
                    />
                ) : null}
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
        </>
    );
}
