'use client';

/**
 * Two-step tenant creation wizard: Country → Tenant Form → processing → success.
 */
import { useMutation } from '@tanstack/react-query';
import { Button, Form, Modal, Steps } from 'antd';
import React, { useCallback, useEffect, useState } from 'react';

import {
  type AdminTenantDetail,
  type CreateAdminTenantRequest,
  createAdminTenant,
} from '@/features/super-admin/api/adminTenants';
import { CreateTenantCountryStep } from '@/features/super-admin/components/CreateTenantCountryStep';
import { CreateTenantProcessingView } from '@/features/super-admin/components/CreateTenantProcessingView';
import { type CreateTenantFormValues } from '@/features/super-admin/components/createTenantFormTypes';
import { OnboardingErrorModal } from '@/features/super-admin/components/OnboardingErrorModal';
import {
  OnboardingSuccessModal,
  type TenantOnboardingSuccessState,
} from '@/features/super-admin/components/OnboardingSuccessModal';
import { TenantFormFields } from '@/features/super-admin/components/TenantFormFields';
import { useTenantCreateFormFields } from '@/features/super-admin/hooks/useTenantCreateFormFields';
import { useTenantOnboardingProgress } from '@/features/super-admin/hooks/useTenantOnboardingProgress';
import type { TenantOnboardingError } from '@/features/super-admin/lib/parseTenantOnboardingError';
import { parseTenantOnboardingError } from '@/features/super-admin/lib/parseTenantOnboardingError';
import { normalizeTenantSlugInput } from '@/features/super-admin/lib/tenantSlug';
import { useCountries } from '@/features/tenancy/hooks/useCountries';
import { useI18n } from '@/i18n';
import { getTenantAppBaseDomain } from '@/lib/auth/impersonationHandoff';

export type { CreateTenantFormValues };

export type CreateTenantWizardProps = {
  open: boolean;
  onClose: () => void;
  onCreated?: (detail: AdminTenantDetail) => void;
  onCreateAnother?: () => void;
  onSwitchToTenant?: (tenantId: string) => void;
  switchToTenantLoading?: boolean;
};

type WizardPhase = 'country' | 'form' | 'processing' | 'processingDone';

const COUNTRY_DEFAULTS = {
  countryCode: 'AT',
  vatRegime: 'AT_RKSV_STANDARD',
  grantTrialLicense: true,
  trialDurationDays: 14,
  importDemoProducts: true,
} as const;

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
  const countriesQuery = useCountries();
  const countries = countriesQuery.data ?? [];
  const [form] = Form.useForm<CreateTenantFormValues & { formError?: string }>();
  const [success, setSuccess] = useState<TenantOnboardingSuccessState | null>(null);
  const [phase, setPhase] = useState<WizardPhase>('country');
  const [onboardingError, setOnboardingError] = useState<TenantOnboardingError | null>(null);
  const [errorContext, setErrorContext] = useState<{ companyName: string; slug: string } | null>(
    null
  );
  const [processingContext, setProcessingContext] = useState<{
    name: string;
    slug: string;
    contactEmail: string;
    grantTrialLicense: boolean;
  } | null>(null);

  const formFields = useTenantCreateFormFields(form, open && phase === 'form');
  const { canSubmit } = formFields;

  const grantTrialLicense = processingContext?.grantTrialLicense ?? true;
  const progressPhase =
    phase === 'processingDone' ? 'success' : phase === 'processing' ? 'running' : 'idle';
  const { definitions, statuses } = useTenantOnboardingProgress(grantTrialLicense, progressPhase);

  const resetFlow = useCallback(() => {
    setPhase('country');
    setProcessingContext(null);
    setOnboardingError(null);
    setErrorContext(null);
  }, []);

  const createMutation = useMutation({
    mutationFn: (body: CreateAdminTenantRequest) => createAdminTenant(body),
    onSuccess: (created) => {
      const contactEmail = created.email?.trim() || processingContext?.contactEmail || '';
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
    }
  }, [open, form, resetFlow, phase]);

  useEffect(() => {
    if (open) {
      form.setFieldsValue({ ...COUNTRY_DEFAULTS });
    }
  }, [open, form]);

  const isProcessing =
    phase === 'processing' || phase === 'processingDone' || createMutation.isPending;

  const handleWizardClose = () => {
    if (phase === 'processing') {
      return;
    }
    onClose();
  };

  const goToFormStep = () => {
    void form.validateFields(['countryCode', 'vatRegime']).then(
      () => setPhase('form'),
      () => undefined
    );
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
      countryCode: values.countryCode,
      vatRegime: values.vatRegime,
      email: values.email.trim(),
      adminEmail: values.email.trim(),
      phone: values.phone?.trim() || undefined,
      address: values.address?.trim() || undefined,
      grantTrialLicense: grantTrial,
      trialDurationDays: grantTrial ? (values.trialDurationDays ?? 14) : undefined,
      importDemoMenu: values.importDemoProducts ?? true,
    });
  };

  const handleFinish = (values: CreateTenantFormValues) => {
    if (phase === 'country') {
      setPhase('form');
      return;
    }
    submitFromForm(values);
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
    [form]
  );

  const modalTitle =
    phase === 'processing' || phase === 'processingDone'
      ? t('tenants.create.processing.title')
      : t('tenants.create.title');

  const stepItems = [
    { title: t('superadmin.tenantCreate.countryStep.title') },
    { title: t('superadmin.tenantCreate.countryStep.tenantFormTitle') },
  ];
  const stepCurrent = phase === 'country' ? 0 : 1;
  const formOpen = open && !success && (phase === 'country' || phase === 'form');

  return (
    <>
      <Modal
        title={modalTitle}
        open={open && !success && phase !== 'country' && phase !== 'form' && isProcessing}
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
        open={formOpen}
        onCancel={handleWizardClose}
        width={640}
        destroyOnHidden
        footer={
          phase === 'country'
            ? [
                <Button key="cancel" htmlType="button" onClick={handleWizardClose}>
                  {t('common.buttons.cancel')}
                </Button>,
                <Button key="next" type="primary" htmlType="button" onClick={goToFormStep}>
                  {t('superadmin.tenantCreate.countryStep.next')}
                </Button>,
              ]
            : [
                <Button key="back" htmlType="button" onClick={() => setPhase('country')}>
                  {t('superadmin.tenantCreate.countryStep.back')}
                </Button>,
                <Button key="cancel" htmlType="button" onClick={handleWizardClose}>
                  {t('common.buttons.cancel')}
                </Button>,
                <Button
                  key="submit"
                  type="primary"
                  htmlType="button"
                  disabled={!canSubmit}
                  onClick={() => form.submit()}
                >
                  {t('tenants.create.submit')}
                </Button>,
              ]
        }
      >
        <Steps current={stepCurrent} items={stepItems} style={{ marginBottom: 24 }} />
        <Form
          form={form}
          layout="vertical"
          requiredMark="optional"
          initialValues={COUNTRY_DEFAULTS}
          onFinish={handleFinish}
        >
          <div
            data-testid="create-tenant-country-panel"
            style={{ display: phase === 'country' ? 'block' : 'none' }}
          >
            <CreateTenantCountryStep
              countries={countries}
              loading={Boolean(countriesQuery.isLoading)}
            />
          </div>
          <div
            data-testid="create-tenant-form-panel"
            style={{ display: phase === 'form' ? 'block' : 'none' }}
          >
            <TenantFormFields form={form} open={open} fieldState={formFields} />
          </div>
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
