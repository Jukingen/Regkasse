'use client';

import type { FormInstance } from 'antd';
import { Card, Checkbox, Col, Form, Input, Row, Space, Typography } from 'antd';
import React, { useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';

import { CreateTenantFormField } from '@/features/super-admin/components/CreateTenantFormField';
import type { CreateTenantWizardData } from '@/features/super-admin/components/CreateTenantWizard/types';
import { TenantSlugFieldExtras } from '@/features/super-admin/components/TenantSlugFieldExtras';
import { useTenantCreateFormFields } from '@/features/super-admin/hooks/useTenantCreateFormFields';
import { listIndustryTemplates } from '@/features/users/api/industryTemplatesApi';
import { useSlugGenerator } from '@/hooks/useSlugGenerator';

const FALLBACK_INDUSTRY_OPTIONS = [
  { id: 'none', name: 'None', description: '' },
  { id: 'restaurant', name: 'Restaurant', description: '' },
  { id: 'retail', name: 'Retail', description: '' },
  { id: 'hotel', name: 'Hotel', description: '' },
];

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

  const templatesQuery = useQuery({
    queryKey: ['industry-templates'],
    queryFn: listIndustryTemplates,
    enabled: open,
  });

  const industryOptions =
    templatesQuery.data && templatesQuery.data.length > 0
      ? [
          { id: 'none', name: t('tenants.create.industry.none'), description: '' },
          ...templatesQuery.data,
        ]
      : FALLBACK_INDUSTRY_OPTIONS.map((o) =>
          o.id === 'none' ? { ...o, name: t('tenants.create.industry.none') } : o
        );

  useEffect(() => {
    form.setFieldsValue({
      name: data.name,
      slug: data.slug,
      email: data.email,
      phone: data.phone,
      address: data.address,
      industryTemplateId: data.industryTemplateId,
      seedIndustryStarterUsers: data.seedIndustryStarterUsers,
    });
  }, [
    form,
    data.name,
    data.slug,
    data.email,
    data.phone,
    data.address,
    data.industryTemplateId,
    data.seedIndustryStarterUsers,
  ]);

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
          industryTemplateId: all.industryTemplateId ?? 'none',
          seedIndustryStarterUsers: Boolean(all.seedIndustryStarterUsers),
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

      <Typography.Title level={5} style={{ marginTop: 8 }}>
        {t('tenants.create.industry.title')}
      </Typography.Title>
      <Typography.Paragraph type="secondary">
        {t('tenants.create.industry.intro')}
      </Typography.Paragraph>
      <Form.Item name="industryTemplateId" style={{ marginBottom: 12 }}>
        <Row gutter={[12, 12]}>
          {industryOptions.map((opt) => {
            const selected = (data.industryTemplateId || 'none') === opt.id;
            return (
              <Col xs={24} sm={12} key={opt.id}>
                <Card
                  size="small"
                  hoverable
                  onClick={() => {
                    form.setFieldsValue({ industryTemplateId: opt.id });
                    onUpdate({ industryTemplateId: opt.id });
                  }}
                  style={{
                    borderColor: selected ? '#1677ff' : undefined,
                    background: selected ? 'rgba(22, 119, 255, 0.04)' : undefined,
                  }}
                >
                  <Typography.Text strong>{opt.name}</Typography.Text>
                  {opt.description ? (
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0, marginTop: 4 }}>
                      {opt.description}
                    </Typography.Paragraph>
                  ) : null}
                </Card>
              </Col>
            );
          })}
        </Row>
      </Form.Item>
      <Form.Item name="seedIndustryStarterUsers" valuePropName="checked">
        <Checkbox>{t('tenants.create.industry.seedStarters')}</Checkbox>
      </Form.Item>
    </Form>
  );
}
