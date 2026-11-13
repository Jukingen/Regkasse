'use client';

import { Alert, Spin } from 'antd';
import Link from 'next/link';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import type { CompanySettings } from '@/api/generated/model';
import { useCompanySettings } from '@/features/settings/hooks/useCompanySettings';
import type { AdminTenantDetail } from '@/features/super-admin/api/adminTenants';
import { TenantCountryFiscalRegimeCard } from '@/features/super-admin/components/TenantCountryFiscalRegimeCard';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

function toCountryCardTenant(settings: CompanySettings): AdminTenantDetail {
  return {
    id: settings.tenantId,
    name: settings.companyName,
    slug: '',
    status: 'active',
    isActive: true,
    createdAt: settings.createdAt,
    country: settings.country,
    vatRegime: settings.vatRegime,
    vatId: settings.vatId ?? settings.companyTaxNumber,
    billingCountry: settings.billingCountry,
    taxExempt: settings.taxExempt ?? false,
  };
}

export function TenantCountryProfilePage() {
  const { t } = useI18n();
  const settingsQuery = useCompanySettings();
  const breadcrumbs = [adminOverviewCrumb(t), { title: t('tenantCountry.title') }];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader title={t('tenantCountry.title')} breadcrumbs={breadcrumbs} />
      {settingsQuery.isLoading ? <Spin /> : null}
      {settingsQuery.isError ? (
        <Alert type="error" title={t('common.errorGeneric')} />
      ) : null}
      {settingsQuery.data ? (
        <TenantCountryFiscalRegimeCard tenant={toCountryCardTenant(settingsQuery.data)} />
      ) : null}
      <Link href="/profile">{t('nav.myProfile')}</Link>
    </div>
  );
}
