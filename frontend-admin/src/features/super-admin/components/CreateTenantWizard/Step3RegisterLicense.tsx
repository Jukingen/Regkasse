'use client';

import React, { useEffect } from 'react';
import { Checkbox, DatePicker, Form, Input, Radio } from 'antd';
import type { FormInstance } from 'antd';
import dayjs from 'dayjs';

import { CreateTenantFormField } from '@/features/super-admin/components/CreateTenantFormField';
import type {
    CreateTenantWizardData,
    LicenseDaysOption,
} from '@/features/super-admin/components/CreateTenantWizard/types';
import { DEFAULT_REGISTER_NUMBER } from '@/features/super-admin/components/CreateTenantWizard/types';
import { useI18n } from '@/i18n';

export type Step3RegisterLicenseProps = {
    form: FormInstance<CreateTenantWizardData>;
    data: CreateTenantWizardData;
    onUpdate: (patch: Partial<CreateTenantWizardData>) => void;
};

const LICENSE_OPTIONS: LicenseDaysOption[] = [30, 90, 365];

/** @deprecated Prefer Step3RegisterLicense */
export type Step3CashRegisterLicenseProps = Step3RegisterLicenseProps;

export function Step3RegisterLicense({ form, data, onUpdate }: Step3RegisterLicenseProps) {
    const { t } = useI18n();

    useEffect(() => {
        form.setFieldsValue({
            registerNumber: data.registerNumber || DEFAULT_REGISTER_NUMBER,
            licenseDays: data.licenseDays,
            licenseStartDate: data.licenseStartDate,
            importDemoProducts: data.importDemoProducts,
        });
    }, [form, data.registerNumber, data.licenseDays, data.licenseStartDate, data.importDemoProducts]);

    return (
        <Form
            form={form}
            layout="vertical"
            requiredMark="optional"
            onValuesChange={(_, all) => {
                const start = all.licenseStartDate as string | dayjs.Dayjs | undefined;
                let licenseStartDate = data.licenseStartDate;
                if (typeof start === 'string') {
                    licenseStartDate = start;
                } else if (start && typeof start.format === 'function') {
                    licenseStartDate = start.format('YYYY-MM-DD');
                }

                const rawDays = all.licenseDays;
                const licenseDays = (
                    typeof rawDays === 'string' ? Number(rawDays) : (rawDays ?? data.licenseDays)
                ) as LicenseDaysOption;

                onUpdate({
                    registerNumber: all.registerNumber ?? DEFAULT_REGISTER_NUMBER,
                    licenseDays: LICENSE_OPTIONS.includes(licenseDays) ? licenseDays : data.licenseDays,
                    licenseStartDate,
                    importDemoProducts: all.importDemoProducts ?? true,
                });
            }}
        >
            <CreateTenantFormField
                name="registerNumber"
                label={t('tenants.create.wizard.fields.registerName')}
                tooltip={t('tenants.create.wizard.fields.registerNumberTooltip')}
                hint={t('tenants.create.wizard.fields.registerNumberHint')}
                required
                rules={[
                    { required: true, message: t('tenants.create.wizard.fields.registerNumberRequired') },
                    { max: 20, message: t('tenants.create.wizard.fields.registerNumberMax') },
                ]}
            >
                <Input placeholder={DEFAULT_REGISTER_NUMBER} maxLength={20} />
            </CreateTenantFormField>

            <Form.Item
                name="licenseDays"
                label={t('tenants.create.wizard.fields.licenseType')}
                tooltip={t('tenants.create.wizard.fields.licenseTypeTooltip')}
                rules={[{ required: true, message: t('tenants.create.wizard.fields.licenseTypeRequired') }]}
            >
                <Radio.Group
                    optionType="button"
                    buttonStyle="solid"
                    options={LICENSE_OPTIONS.map((days) => ({
                        value: days,
                        label: t('tenants.create.wizard.fields.licenseDaysOption', { days }),
                    }))}
                />
            </Form.Item>

            <Form.Item
                name="licenseStartDate"
                label={t('tenants.create.wizard.fields.licenseStartDate')}
                tooltip={t('tenants.create.wizard.fields.licenseStartDateTooltip')}
                rules={[{ required: true, message: t('tenants.create.wizard.fields.licenseStartRequired') }]}
                getValueProps={(value: string | undefined) => ({
                    value: value ? dayjs(value, 'YYYY-MM-DD') : dayjs(),
                })}
                getValueFromEvent={(date: dayjs.Dayjs | null) =>
                    date ? date.format('YYYY-MM-DD') : undefined
                }
            >
                <DatePicker style={{ width: '100%' }} format="YYYY-MM-DD" allowClear={false} />
            </Form.Item>

            <Form.Item
                name="importDemoProducts"
                valuePropName="checked"
                label={t('tenants.create.wizard.fields.createDemoProductsLabel')}
                tooltip={t('tenants.create.fields.importDemoProducts.tooltip')}
            >
                <Checkbox>{t('tenants.create.wizard.fields.createDemoProducts')}</Checkbox>
            </Form.Item>
        </Form>
    );
}

/** @deprecated Use Step3RegisterLicense */
export const Step3CashRegisterLicense = Step3RegisterLicense;
