'use client';

import { Alert, Form, Select, Spin } from 'antd';
import React, { useEffect } from 'react';

import type { CountryProfileSummaryDto } from '@/api/generated/model';
import type { CreateTenantFormValues } from '@/features/super-admin/components/createTenantFormTypes';
import { useI18n } from '@/i18n';

export type CreateTenantCountryStepProps = {
  countries: CountryProfileSummaryDto[];
  loading: boolean;
};

export function CreateTenantCountryStep({ countries, loading }: CreateTenantCountryStepProps) {
  const { t } = useI18n();
  const form = Form.useFormInstance<CreateTenantFormValues>();
  const countryCode = Form.useWatch('countryCode', form);
  const selected = countries.find((country) => country.code === countryCode);
  const regimes = (selected?.allowedVatRegimes ?? []).filter(
    (regime): regime is NonNullable<typeof regime> => Boolean(regime)
  );
  const isNonAt = Boolean(countryCode && countryCode !== 'AT');

  useEffect(() => {
    if (!countryCode) {
      return;
    }
    const match = countries.find((country) => country.code === countryCode);
    const allowed = (match?.allowedVatRegimes ?? []).filter(
      (regime): regime is NonNullable<typeof regime> => Boolean(regime)
    );
    const current = form.getFieldValue('vatRegime') as string | undefined;
    if (!current || !allowed.includes(current as (typeof allowed)[number])) {
      form.setFieldValue('vatRegime', allowed[0]);
    }
  }, [countryCode, countries, form]);

  return (
    <Spin spinning={loading}>
      {isNonAt ? (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          title={t('superadmin.tenantCreate.countryStep.nonAtBanner')}
        />
      ) : null}

      <Form.Item
        name="countryCode"
        label={t('superadmin.tenantCreate.countryStep.countryLabel')}
        rules={[
          { required: true, message: t('superadmin.tenantCreate.countryStep.requiredCountry') },
        ]}
      >
        <Select
          allowClear
          showSearch
          optionFilterProp="label"
          options={countries
            .filter((country) => country.code)
            .map((country) => ({
              value: country.code,
              label: `${country.code} — ${country.name ?? country.code}`,
            }))}
        />
      </Form.Item>

      <Form.Item
        name="vatRegime"
        label={t('superadmin.tenantCreate.countryStep.vatRegimeLabel')}
        rules={[
          { required: true, message: t('superadmin.tenantCreate.countryStep.requiredVatRegime') },
        ]}
      >
        <Select
          disabled={!selected}
          options={regimes.map((regime) => ({ value: regime, label: regime }))}
        />
      </Form.Item>
    </Spin>
  );
}
