'use client';

import { Table } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import React from 'react';

import type { ExpiringLicenseInfo } from '@/api/generated/model';
import { billingApi } from '@/features/billing/api/billingApi';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';
import { dateColumnRender } from '@/components/DateColumn';
import { useI18n } from '@/i18n';

export function BillingExpiringTable() {
  const { t } = useI18n();
  const canAccess = useBillingAccess();

  const expiringQuery = billingApi.useExpiring(
    { daysThreshold: 30 },
    { query: { enabled: canAccess } }
  );

  const columns: ColumnsType<ExpiringLicenseInfo> = [
    {
      title: t('billing.expiring.columns.tenant'),
      key: 'tenant',
      render: (_, row) => (
        <Link href={`/admin/tenants/${row.tenantId}`}>
          {row.tenantName ?? row.tenantSlug ?? row.tenantId}
        </Link>
      ),
    },
    {
      title: t('billing.expiring.columns.licenseKey'),
      dataIndex: 'licenseKey',
      key: 'licenseKey',
      ellipsis: true,
    },
    {
      title: t('billing.expiring.columns.validUntil'),
      dataIndex: 'validUntilUtc',
      key: 'validUntilUtc',
      render: dateColumnRender('datetime'),
    },
    {
      title: t('billing.expiring.columns.daysRemaining'),
      dataIndex: 'daysRemaining',
      key: 'daysRemaining',
      width: 120,
    },
  ];

  return (
    <Table<ExpiringLicenseInfo>
      rowKey={(row) => row.licenseSaleId ?? `${row.tenantId}-${row.licenseKey}`}
      columns={columns}
      dataSource={expiringQuery.data ?? []}
      loading={expiringQuery.isLoading}
      pagination={false}
      size="small"
    />
  );
}
