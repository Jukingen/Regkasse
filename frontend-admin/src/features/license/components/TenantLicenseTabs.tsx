'use client';

import { Tabs } from 'antd';

import { TenantLicenseBillingTab } from '@/features/billing/components/TenantLicenseBillingTab';
import { TenantLicenseOverview } from '@/features/license/components/TenantLicenseOverview';
import { useI18n } from '@/i18n';

export function TenantLicenseTabs() {
  const { t } = useI18n();

  return (
    <Tabs
      defaultActiveKey="sales"
      items={[
        {
          key: 'sales',
          label: t('license.tenant.title'),
          children: <TenantLicenseBillingTab />,
        },
        {
          key: 'overview',
          label: t('license.tenant.statusOverviewTab'),
          children: <TenantLicenseOverview />,
        },
      ]}
    />
  );
}
