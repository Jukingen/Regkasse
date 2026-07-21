'use client';

import type { FormInstance } from 'antd';
import { Form, Input, Space } from 'antd';
import React, { useEffect } from 'react';

import { CreateTenantFormField } from '@/features/super-admin/components/CreateTenantFormField';
import type { CreateTenantWizardData } from '@/features/super-admin/components/CreateTenantWizard/types';
import { TenantSlugFieldExtras } from '@/features/super-admin/components/TenantSlugFieldExtras';
import { useTenantCreateFormFields } from '@/features/super-admin/hooks/useTenantCreateFormFields';
import { useSlugGenerator } from '@/hooks/useSlugGenerator';

export type Step1TenantInfoProps = {
  form: FormInstance<CreateTenantWizardData>;
  open: boolean;
  data: CreateTenantWizardData;
  onUpdate: (patch: Partial<CreateTenantWizardData>) => void;
};

export function Step1TenantInfo({ form, open, data, onUpdate }: Step1TenantInfoProps) {
  const { generateSlug } = useSlugGenerator();
  const fieldState = useTenantCreateFormFields(form, open);
  const {
    t,
    baseDomain,
    slugWatch,
    slugRules,
    slugFieldStatus,
    slugAvailabilityUi,
    portalPreviewUrl,
    nameRules,
    nameFieldStatus,
    emailRules,
    emailFieldStatus,
    phoneRules,
    phoneFieldStatus,
    addressRules,
    addressFieldStatus,
    handleSlugChange,
    handleSlugBlur,
  } = fieldState;

  useEffect(() => {
    form.setFieldsValue({
      name: data.name,
      slug: data.slug,
      email: data.email,
      phone: data.phone,
      address: data.address,
    });
  }, [form, data.name, data.slug, data.email, data.phone, data.address]);

  const handleNameChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const name = event.target.value;
    const currentSlug = form.getFieldValue('slug') as string | undefined;
    const previousAuto = data.name ? generateSlug(data.name) : '';
    const shouldAutoUpdateSlug =
      !currentSlug || currentSlug === previousAuto || currentSlug === data.slug;
    const slug = shouldAutoUpdateSlug ? generateSlug(name) : (currentSlug ?? '');
    form.setFieldsValue({ name, slug });
    onUpdate({ name, slug });
    if (shouldAutoUpdateSlug && slug) {
      void form.validateFields(['slug']);
    }
  };

  return (
    <Form
      form={form}
      layout="vertical"
      requiredMark="optional"
      onValuesChange={(_, all) => {
        onUpdate({
          name: all.name ?? '',
          slug: all.slug ?? '',
          email: all.email ?? '',
          phone: all.phone,
          address: all.address,
        });
      }}
    >
      <CreateTenantFormField
        name="name"
        label={t('tenants.create.fields.name.label')}
        tooltip={t('tenants.create.fields.name.tooltip')}
        hint={t('tenants.create.fields.name.hint')}
        required
        rules={nameRules}
        validateStatus={nameFieldStatus}
      >
        <Input
          onChange={handleNameChange}
          onBlur={fieldState.handleNameBlur}
          placeholder={t('tenants.create.fields.name.placeholder')}
          maxLength={200}
          showCount
        />
      </CreateTenantFormField>

      <CreateTenantFormField
        name="slug"
        label={t('tenants.create.fields.slug.label')}
        tooltip={t('tenants.create.fields.slug.tooltip')}
        hint={t('tenants.create.fields.slug.hint')}
        required
        validateTrigger={['onChange', 'onBlur']}
        validateStatus={slugFieldStatus}
        rules={slugRules}
      >
        <Space orientation="vertical" style={{ width: '100%' }} size={0}>
          <Input
            placeholder={t('tenants.create.fields.slug.placeholder')}
            onChange={handleSlugChange}
            onBlur={handleSlugBlur}
            addonBefore="https://"
            addonAfter={`.${baseDomain}`}
            autoComplete="off"
          />
          <TenantSlugFieldExtras
            slugValue={slugWatch}
            baseDomain={baseDomain}
            portalUrl={portalPreviewUrl}
            availabilityUi={slugAvailabilityUi}
          />
        </Space>
      </CreateTenantFormField>

      <CreateTenantFormField
        name="email"
        label={t('tenants.create.fields.contactEmail.label')}
        tooltip={t('tenants.create.wizard.fields.contactEmailTooltip')}
        hint={t('tenants.create.fields.contactEmail.hint')}
        required
        rules={emailRules}
        validateStatus={emailFieldStatus}
      >
        <Input
          type="email"
          name="email"
          placeholder={t('tenants.create.fields.contactEmail.placeholder')}
          autoComplete="email"
        />
      </CreateTenantFormField>

      <CreateTenantFormField
        name="phone"
        label={t('tenants.create.fields.phone.label')}
        tooltip={t('tenants.create.fields.phone.tooltip')}
        hint={t('tenants.create.fields.phone.hint')}
        rules={phoneRules}
        validateStatus={phoneFieldStatus}
      >
        <Input placeholder={t('tenants.create.fields.phone.placeholder')} autoComplete="tel" />
      </CreateTenantFormField>

      <CreateTenantFormField
        name="address"
        label={t('tenants.create.fields.address.label')}
        tooltip={t('tenants.create.fields.address.tooltip')}
        hint={t('tenants.create.fields.address.hint')}
        rules={addressRules}
        validateStatus={addressFieldStatus}
      >
        <Input.TextArea
          rows={2}
          placeholder={t('tenants.create.fields.address.placeholder')}
          maxLength={500}
          showCount
        />
      </CreateTenantFormField>
    </Form>
  );
}
