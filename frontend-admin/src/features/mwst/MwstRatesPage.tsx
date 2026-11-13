'use client';

import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Select, Space, Table } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useEffect, useState } from 'react';

import {
  useGetApiAdminMwstTenantsTenantIdRates,
  usePostApiAdminMwstCanaryRollback,
} from '@/api/generated/admin/admin';
import type { ChMwstRateDto } from '@/api/generated/model';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

type RateRow = ChMwstRateDto & { key: string };

export function MwstRatesPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const [tenantId, setTenantId] = useState<string | undefined>();
  const [picked, setPicked] = useState(false);

  const tenants = useQuery({
    queryKey: ['admin', 'mwst', 'tenants'],
    queryFn: () => listAdminTenants(false),
  });

  useEffect(() => {
    if (picked) return;
    const first = tenants.data?.[0]?.id;
    if (first) {
      setTenantId(first);
      setPicked(true);
    }
  }, [picked, tenants.data]);

  const rates = useGetApiAdminMwstTenantsTenantIdRates(tenantId ?? '', {
    query: { enabled: Boolean(tenantId) },
  });

  const rollback = usePostApiAdminMwstCanaryRollback({
    mutation: {
      onSuccess: () => {
        notify.success(t('admin.mwst.rolledBack'));
      },
      onError: (err) => {
        notify.apiError(err, {
          logContext: 'Mwst.rollback',
          fallbackKey: 'admin.mwst.loadError',
        });
      },
    },
  });

  const sourceLabel = (source: string | null | undefined) => {
    if (source === 'seed') return t('admin.mwst.sourceSeed');
    if (source === 'tenant_override') return t('admin.mwst.sourceOverride');
    return t('admin.mwst.sourceUnknown');
  };
  const appliesLabel = rates.data?.applies ? t('admin.mwst.appliesYes') : t('admin.mwst.appliesNo');
  const rows: RateRow[] = (rates.data?.rates ?? []).map((row, index) => ({
    ...row,
    key: `${row.code ?? 'rate'}-${index}`,
  }));

  const columns: ColumnsType<RateRow> = [
    { title: t('admin.mwst.colCode'), dataIndex: 'code', render: (value) => value || '—' },
    { title: t('admin.mwst.colLabel'), dataIndex: 'label', render: (value) => value || '—' },
    { title: t('admin.mwst.colRate'), dataIndex: 'rate' },
    { title: t('admin.mwst.colEffectiveFrom'), dataIndex: 'effectiveFrom' },
    {
      title: t('admin.mwst.colSource'),
      key: 'source',
      render: () => sourceLabel(rates.data?.source),
    },
    {
      title: t('admin.mwst.colApplies'),
      key: 'applies',
      render: () => appliesLabel,
    },
  ];

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <AdminPageHeader title={t('admin.mwst.title')} subtitle={t('admin.mwst.subtitle')} />
      {rates.isError || tenants.isError ? (
        <Alert type="error" showIcon title={t('admin.mwst.loadError')} />
      ) : null}
      <Space wrap>
        <Select
          aria-label={t('admin.mwst.tenantFilter')}
          placeholder={t('admin.mwst.tenantFilter')}
          style={{ minWidth: 280 }}
          loading={tenants.isLoading}
          value={tenantId}
          options={(tenants.data ?? []).map((tenant) => ({
            value: tenant.id,
            label: `${tenant.name} (${tenant.slug})`,
          }))}
          onChange={(value: string) => {
            setPicked(true);
            setTenantId(value);
          }}
        />
        <Button danger disabled={!tenantId} loading={rollback.isPending} onClick={() => rollback.mutate()}>
          {t('admin.mwst.rollback')}
        </Button>
      </Space>
      {!tenantId ? <span>{t('admin.mwst.missingTenant')}</span> : null}
      <Table<RateRow>
        rowKey="key"
        columns={columns}
        dataSource={rows}
        loading={rates.isLoading}
        locale={{ emptyText: t('admin.mwst.empty') }}
        pagination={false}
      />
    </Space>
  );
}
