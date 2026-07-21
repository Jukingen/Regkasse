'use client';

import { Button, DatePicker, Space, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import React, { useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { billingApi } from '@/features/billing/api/billingApi';
import { BillingAccessGate } from '@/features/billing/components/BillingAccessGate';
import { BillingStatsCards } from '@/features/billing/components/BillingStatsCards';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export function BillingStatsPage() {
  const { t } = useI18n();
  const canAccess = useBillingAccess();
  const [range, setRange] = useState<{ fromDate?: string; toDate?: string }>({});

  const statsQuery = billingApi.useStats(
    { fromDate: range.fromDate, toDate: range.toDate },
    { query: { enabled: canAccess } }
  );

  const onRangeChange = (values: [Dayjs | null, Dayjs | null] | null) => {
    setRange({
      fromDate: values?.[0]?.startOf('day').toISOString(),
      toDate: values?.[1]?.endOf('day').toISOString(),
    });
  };

  return (
    <BillingAccessGate>
      <AdminPageShell>
        <AdminPageHeader
          title={t('billing.stats.pageTitle')}
          breadcrumbs={[
            adminOverviewCrumb(t),
            { title: t('nav.licenseHub'), href: '/admin/billing' },
            { title: t('billing.stats.pageTitle') },
          ]}
          actions={
            <Space wrap>
              <DatePicker.RangePicker
                onChange={onRangeChange}
                defaultPickerValue={[dayjs().startOf('month'), dayjs()]}
              />
              <Button type="primary" onClick={() => statsQuery.refetch()}>
                {t('billing.stats.applyRange')}
              </Button>
            </Space>
          }
        />
        <Typography.Paragraph type="secondary">
          {t('billing.stats.pageSubtitle')}
        </Typography.Paragraph>
        <BillingStatsCards stats={statsQuery.data} loading={statsQuery.isLoading} />
      </AdminPageShell>
    </BillingAccessGate>
  );
}
