'use client';

/**
 * Super-admin tenant create form body: live validation, slug availability, collapsible optional fields.
 */
import React from 'react';
import { Checkbox, Collapse, Divider, Form, Input, Space, Typography } from 'antd';
import type { FormInstance } from 'antd';

import { CreateTenantFormField } from '@/features/super-admin/components/CreateTenantFormField';
import { TenantAdminAccessPreview } from '@/features/super-admin/components/TenantAdminAccessPreview';
import { TenantSlugFieldExtras } from '@/features/super-admin/components/TenantSlugFieldExtras';
import { useTenantCreateFormFields } from '@/features/super-admin/hooks/useTenantCreateFormFields';
import type { CreateTenantFormValues } from '@/features/super-admin/components/CreateTenantModal';
import styles from '@/styles/tenant-form.module.css';

export type TenantFormFieldsProps = {
    form: FormInstance<CreateTenantFormValues & { formError?: string }>;
    open: boolean;
};

export function TenantFormFields({ form, open }: TenantFormFieldsProps) {
    const fields = useTenantCreateFormFields(form, open);
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
        handleNameBlur,
    } = fields;

    return (
        <>
            <Form.Item name="formError" hidden>
                <Input />
            </Form.Item>

            <Form.Item shouldUpdate noStyle>
                {() => {
                    const errors = form.getFieldError('formError');
                    return errors.length > 0 ? (
                        <div className={styles.error} style={{ marginBottom: 16 }}>
                            {errors[0]}
                        </div>
                    ) : null;
                }}
            </Form.Item>

            <Typography.Title level={5} style={{ marginTop: 0 }}>
                {t('tenants.create.sections.basic')}
            </Typography.Title>

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
                    onBlur={handleNameBlur}
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
                <Space direction="vertical" style={{ width: '100%' }} size={0}>
                    <Input
                        placeholder={t('tenants.create.fields.slug.placeholder')}
                        onChange={handleSlugChange}
                        onBlur={handleSlugBlur}
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

            <TenantAdminAccessPreview slugValue={slugWatch} />

            <Divider style={{ margin: '8px 0 16px' }} />

            <Typography.Title level={5}>{t('tenants.create.sections.contact')}</Typography.Title>

            <CreateTenantFormField
                name="email"
                label={t('tenants.create.fields.contactEmail.label')}
                tooltip={t('tenants.create.fields.contactEmail.tooltip')}
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

            <Collapse
                ghost
                style={{ marginBottom: 8 }}
                items={[
                    {
                        key: 'advanced',
                        label: t('tenants.create.sections.advanced'),
                        children: (
                            <>
                                <CreateTenantFormField
                                    name="phone"
                                    label={t('tenants.create.fields.phone.label')}
                                    tooltip={t('tenants.create.fields.phone.tooltip')}
                                    hint={t('tenants.create.fields.phone.hint')}
                                    rules={phoneRules}
                                    validateStatus={phoneFieldStatus}
                                >
                                    <Input
                                        placeholder={t('tenants.create.fields.phone.placeholder')}
                                        autoComplete="tel"
                                    />
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
                            </>
                        ),
                    },
                ]}
            />

            <Divider style={{ margin: '8px 0 16px' }} />

            <Form.Item
                name="grantTrialLicense"
                valuePropName="checked"
                tooltip={t('tenants.create.fields.grantTrialLicense.tooltip')}
                style={{ marginBottom: 8 }}
            >
                <Checkbox>
                    <span>
                        {t('tenants.create.fields.grantTrialLicense.label')}
                        <span className={styles.subtext}>{t('tenants.create.fields.grantTrialLicense.subtext')}</span>
                    </span>
                </Checkbox>
            </Form.Item>
        </>
    );
}
