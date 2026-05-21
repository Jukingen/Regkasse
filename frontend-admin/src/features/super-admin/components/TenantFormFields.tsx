'use client';

/**
 * Super-admin tenant create form: operator-friendly fields, live subdomain check, automation toggles.
 */
import React from 'react';
import { Checkbox, Collapse, Divider, Form, Input, Space } from 'antd';
import type { FormInstance } from 'antd';

import { CreateTenantFormField } from '@/features/super-admin/components/CreateTenantFormField';
import { TenantSlugFieldExtras } from '@/features/super-admin/components/TenantSlugFieldExtras';
import type { useTenantCreateFormFields } from '@/features/super-admin/hooks/useTenantCreateFormFields';
import type { CreateTenantFormValues } from '@/features/super-admin/components/CreateTenantModal';
import styles from '@/styles/tenant-form.module.css';

export type TenantFormFieldsProps = {
    form: FormInstance<CreateTenantFormValues & { formError?: string }>;
    open: boolean;
    fieldState: ReturnType<typeof useTenantCreateFormFields>;
};

export function TenantFormFields({ form, fieldState }: TenantFormFieldsProps) {
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
    } = fieldState;

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

            <Divider style={{ margin: '4px 0 12px' }} />

            <Form.Item
                name="grantTrialLicense"
                valuePropName="checked"
                tooltip={t('tenants.create.fields.grantTrialLicense.tooltip')}
                style={{ marginBottom: 4 }}
            >
                <Checkbox>
                    <span>{t('tenants.create.fields.grantTrialLicense.label')}</span>
                </Checkbox>
            </Form.Item>

            <Form.Item
                name="autoDemoSetup"
                valuePropName="checked"
                tooltip={t('tenants.create.fields.autoDemoSetup.tooltip')}
                style={{ marginBottom: 8 }}
            >
                <Checkbox disabled>
                    <span>{t('tenants.create.fields.autoDemoSetup.label')}</span>
                </Checkbox>
            </Form.Item>

            <Collapse
                ghost
                style={{ marginBottom: 0 }}
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

        </>
    );
}
