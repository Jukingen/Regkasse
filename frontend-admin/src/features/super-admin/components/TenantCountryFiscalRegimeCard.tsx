'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Descriptions, Form, Select, Space, Typography } from 'antd';
import { useEffect, useMemo, useState } from 'react';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import {
  updateAdminTenantCountry,
  type AdminTenantDetail,
} from '@/features/super-admin/api/adminTenants';
import { invalidateTenantLifecycleQueries } from '@/features/super-admin/utils/invalidateTenantLifecycleQueries';
import { useCountries } from '@/features/tenancy/hooks/useCountries';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

export type TenantCountryFiscalRegimeCardProps = {
  tenant: AdminTenantDetail;
  onUpdated?: () => void;
};

type CountryFormValues = {
  country: string;
  vatRegime: string;
};

function displayOrEmpty(value: string | null | undefined, empty: string): string {
  const trimmed = value?.trim();
  return trimmed ? trimmed : empty;
}

export function TenantCountryFiscalRegimeCard({
  tenant,
  onUpdated,
}: TenantCountryFiscalRegimeCardProps) {
  const { t } = useI18n();
  const { user } = useAuth();
  const { modal } = useAntdApp();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const canEdit = isSuperAdmin(user?.role);
  const [editing, setEditing] = useState(false);
  const [form] = Form.useForm<CountryFormValues>();
  const { data: countries = [], isLoading: countriesLoading } = useCountries();
  const countryWatch = Form.useWatch('country', form);

  const selected = useMemo(
    () => countries.find((country) => country.code === countryWatch),
    [countries, countryWatch]
  );
  const regimes = (selected?.allowedVatRegimes ?? []).filter(
    (regime): regime is NonNullable<typeof regime> => Boolean(regime)
  );

  useEffect(() => {
    if (!editing || !countryWatch) {
      return;
    }
    const allowed = (selected?.allowedVatRegimes ?? []).filter(
      (regime): regime is NonNullable<typeof regime> => Boolean(regime)
    );
    const current = form.getFieldValue('vatRegime') as string | undefined;
    if (!current || !allowed.includes(current as (typeof allowed)[number])) {
      form.setFieldValue('vatRegime', allowed[0]);
    }
  }, [countryWatch, editing, form, selected]);

  const saveMutation = useMutation({
    mutationFn: (values: CountryFormValues) =>
      updateAdminTenantCountry(tenant.id, {
        country: values.country,
        vatRegime: values.vatRegime,
      }),
    onSuccess: () => {
      notify.successKey('tenantCountry.success');
      setEditing(false);
      invalidateTenantLifecycleQueries(queryClient, tenant.id);
      onUpdated?.();
    },
    onError: (err: unknown) => {
      const code =
        typeof err === 'object' && err !== null
          ? (err as { response?: { data?: { code?: string } } }).response?.data?.code
          : undefined;
      if (code === 'COUNTRY_LOCKED_FISCAL') {
        notify.errorKey('tenantCountry.lockedFiscal');
        return;
      }
      notify.apiError(err, {
        logContext: 'TenantCountryFiscalRegimeCard.save',
        fallbackKey: 'tenantCountry.saveFailed',
      });
    },
  });

  const empty = t('tenantCountry.empty');
  const startEdit = () => {
    form.setFieldsValue({
      country: tenant.country ?? 'AT',
      vatRegime: tenant.vatRegime ?? 'AT_RKSV_STANDARD',
    });
    setEditing(true);
  };

  const confirmSave = (values: CountryFormValues) => {
    modal.confirm({
      title: t('tenantCountry.confirmTitle'),
      content: t('tenantCountry.confirmBody'),
      okText: t('tenantCountry.confirmOk'),
      cancelText: t('tenantCountry.cancel'),
      onOk: () => saveMutation.mutateAsync(values),
    });
  };

  return (
    <Card
      data-testid="tenant-country-card"
      title={t('tenantCountry.title')}
      extra={
        canEdit && !editing ? (
          <Button data-testid="tenant-country-edit" type="link" onClick={startEdit}>
            {t('tenantCountry.edit')}
          </Button>
        ) : null
      }
    >
      {editing ? (
        <Form
          form={form}
          layout="vertical"
          onFinish={confirmSave}
          initialValues={{
            country: tenant.country ?? 'AT',
            vatRegime: tenant.vatRegime ?? 'AT_RKSV_STANDARD',
          }}
        >
          <Form.Item
            name="country"
            label={t('tenantCountry.country')}
            rules={[{ required: true, message: t('tenantCountry.country') }]}
          >
            <Select
              showSearch
              loading={countriesLoading}
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
            label={t('tenantCountry.vatRegime')}
            rules={[{ required: true, message: t('tenantCountry.vatRegime') }]}
          >
            <Select
              disabled={!selected}
              options={regimes.map((regime) => ({ value: regime, label: regime }))}
            />
          </Form.Item>
          <Space>
            <Button
              data-testid="tenant-country-save"
              type="primary"
              htmlType="submit"
              loading={saveMutation.isPending}
            >
              {t('tenantCountry.save')}
            </Button>
            <Button onClick={() => setEditing(false)}>{t('tenantCountry.cancel')}</Button>
          </Space>
        </Form>
      ) : (
        <>
          <Descriptions column={{ xs: 1, sm: 2 }} size="small">
            <Descriptions.Item label={t('tenantCountry.country')}>
              {displayOrEmpty(tenant.country, empty)}
            </Descriptions.Item>
            <Descriptions.Item label={t('tenantCountry.vatRegime')}>
              {displayOrEmpty(tenant.vatRegime, empty)}
            </Descriptions.Item>
            <Descriptions.Item label={t('tenantCountry.vatId')}>
              {displayOrEmpty(tenant.vatId, empty)}
            </Descriptions.Item>
            <Descriptions.Item label={t('tenantCountry.billingCountry')}>
              {displayOrEmpty(tenant.billingCountry, empty)}
            </Descriptions.Item>
            <Descriptions.Item label={t('tenantCountry.taxExempt')}>
              {tenant.taxExempt ? t('tenantCountry.taxExemptYes') : t('tenantCountry.taxExemptNo')}
            </Descriptions.Item>
          </Descriptions>
          {!canEdit ? (
            <Typography.Paragraph type="secondary" data-testid="tenant-country-view-only">
              {t('tenantCountry.viewOnly')}
            </Typography.Paragraph>
          ) : null}
        </>
      )}
    </Card>
  );
}
