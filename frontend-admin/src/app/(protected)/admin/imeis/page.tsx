'use client';

import { Select, Space } from 'antd';
import React, { useMemo, useState } from 'react';

import { useGetApiAdminProducts } from '@/api/generated/admin/admin';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { useAdminImeis } from '@/features/imeis/api';
import { ImeiTable, type ImeiRow } from '@/features/imeis/ImeiTable';
import { useI18n } from '@/i18n';

const STATUSES = ['InStock', 'Sold', 'Returned'] as const;

export default function AdminImeisPage() {
  const { t } = useI18n();
  const [productId, setProductId] = useState<string | undefined>();
  const [status, setStatus] = useState<string | undefined>();
  const products = useGetApiAdminProducts({ Page: 1, PageSize: 100 });
  const query = useAdminImeis({ productId, status });
  const rows = useMemo<ImeiRow[]>(
    () =>
      (query.data ?? []).map((row) => ({
        id: row.id,
        imei: row.imei,
        productName: row.productName || '—',
        status: row.status,
        soldAt: row.soldAtUtc || '—',
        warrantyMonths: row.warrantyMonths,
      })),
    [query.data]
  );

  return (
    <AdminPageShell>
      <AdminPageHeader title={t('admin.imeis.title')} subtitle={t('admin.imeis.subtitle')} />
      <Space wrap>
        <Select
          allowClear
          style={{ minWidth: 220 }}
          placeholder={t('admin.imeis.product')}
          value={productId}
          onChange={(value) => setProductId(value)}
          options={(products.data?.items ?? []).flatMap((product) =>
            product.id ? [{ value: product.id, label: product.name ?? product.id }] : []
          )}
        />
        <Select
          allowClear
          style={{ minWidth: 180 }}
          placeholder={t('admin.imeis.status')}
          value={status}
          onChange={(value) => setStatus(value)}
          options={STATUSES.map((value) => ({
            value,
            label: t(`admin.imeis.status${value}`),
          }))}
        />
      </Space>
      <ImeiTable rows={rows} loading={query.isLoading} />
    </AdminPageShell>
  );
}
