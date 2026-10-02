'use client';

import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Descriptions, Input, Select, Space, Table, Tabs } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useEffect, useState } from 'react';

import {
  useGetApiAdminKassensicherheitRecentTransactions,
  useGetApiAdminKassensicherheitStatus,
  usePostApiAdminKassensicherheitExportDsfinvk,
} from '@/api/generated/admin/admin';
import type {
  KassenSicherheitExportRequest,
  KassenSicherheitExportResultDto,
  KassenSicherheitRecentTransactionDto,
} from '@/api/generated/model';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

function artifactUrl(result: KassenSicherheitExportResultDto): string | null {
  const extra = result as KassenSicherheitExportResultDto & {
    url?: string | null;
    downloadUrl?: string | null;
    artifactUrl?: string | null;
  };
  const url = extra.url ?? extra.downloadUrl ?? extra.artifactUrl;
  return typeof url === 'string' && url.length > 0 ? url : null;
}

function downloadArtifact(url: string) {
  const link = document.createElement('a');
  link.href = url;
  link.rel = 'noopener';
  link.target = '_blank';
  document.body.appendChild(link);
  link.click();
  link.remove();
}

export function KassenSicherheitPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const [tenantId, setTenantId] = useState<string | undefined>();
  const [picked, setPicked] = useState(false);
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');

  const tenants = useQuery({
    queryKey: ['admin', 'kassensicherheit', 'tenants'],
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

  const status = useGetApiAdminKassensicherheitStatus(tenantId ? { tenantId } : undefined, {
    query: { enabled: Boolean(tenantId) },
  });
  const recent = useGetApiAdminKassensicherheitRecentTransactions(
    tenantId ? { tenantId } : undefined,
    { query: { enabled: Boolean(tenantId) } },
  );

  const exportDsfinvk = usePostApiAdminKassensicherheitExportDsfinvk({
    mutation: {
      onSuccess: (data) => {
        const url = artifactUrl(data);
        if (url) downloadArtifact(url);
        notify.success(
          data.status === 'PENDING' ? t('admin.kassensicherheit.exportPending') : (data.status ?? ''),
        );
      },
      onError: (err) => {
        notify.apiError(err, {
          logContext: 'KassenSicherheit.export',
          fallbackKey: 'admin.kassensicherheit.loadError',
        });
      },
    },
  });

  const columns: ColumnsType<KassenSicherheitRecentTransactionDto> = [
    {
      title: t('admin.kassensicherheit.colTxId'),
      dataIndex: 'transactionId',
      render: (value: string | null | undefined) => value || '—',
    },
    { title: t('admin.kassensicherheit.colReceipt'), dataIndex: 'receiptNumber' },
    {
      title: t('admin.kassensicherheit.colStatus'),
      dataIndex: 'status',
      render: (value: string | null | undefined) => value || '—',
    },
    { title: t('admin.kassensicherheit.colTime'), dataIndex: 'createdAt' },
  ];

  const requestExport = () => {
    if (!tenantId || !start || !end) return;
    exportDsfinvk.mutate({
      data: { tenantId, start, end } as KassenSicherheitExportRequest,
    });
  };

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <AdminPageHeader
        title={t('admin.kassensicherheit.title')}
        subtitle={t('admin.kassensicherheit.subtitle')}
      />
      {status.isError || recent.isError || tenants.isError ? (
        <Alert type="error" showIcon title={t('admin.kassensicherheit.loadError')} />
      ) : null}
      <Select
        aria-label={t('admin.kassensicherheit.tenantFilter')}
        placeholder={t('admin.kassensicherheit.tenantFilter')}
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
      {!tenantId ? <span>{t('admin.kassensicherheit.missingTenant')}</span> : null}
      <Space wrap>
        <label>
          {t('admin.kassensicherheit.start')}
          <Input
            type="date"
            aria-label={t('admin.kassensicherheit.start')}
            value={start}
            onChange={(event) => setStart(event.target.value)}
          />
        </label>
        <label>
          {t('admin.kassensicherheit.end')}
          <Input
            type="date"
            aria-label={t('admin.kassensicherheit.end')}
            value={end}
            onChange={(event) => setEnd(event.target.value)}
          />
        </label>
        <Button
          disabled={!tenantId || !start || !end}
          loading={exportDsfinvk.isPending}
          onClick={requestExport}
        >
          {t('admin.kassensicherheit.export')}
        </Button>
      </Space>
      <Tabs
        items={[
          {
            key: 'status',
            label: t('admin.kassensicherheit.tabStatus'),
            children: (
              <Descriptions column={1}>
                <Descriptions.Item label={t('admin.kassensicherheit.provider')}>
                  {status.data?.provider || '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('admin.kassensicherheit.environment')}>
                  {status.data?.environment || '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('admin.kassensicherheit.flagEnabled')}>
                  {status.data?.flagEnabled
                    ? t('admin.kassensicherheit.flagOn')
                    : t('admin.kassensicherheit.flagOff')}
                </Descriptions.Item>
                <Descriptions.Item label={t('admin.kassensicherheit.hasOverride')}>
                  {status.data?.hasTenantOverride
                    ? t('admin.kassensicherheit.overrideYes')
                    : t('admin.kassensicherheit.overrideNo')}
                </Descriptions.Item>
                <Descriptions.Item label={t('admin.kassensicherheit.tssId')}>
                  {status.data?.deTssId || '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('admin.kassensicherheit.clientId')}>
                  {status.data?.deClientId || '—'}
                </Descriptions.Item>
              </Descriptions>
            ),
          },
          {
            key: 'recent',
            label: t('admin.kassensicherheit.tabRecent'),
            children: (
              <Table<KassenSicherheitRecentTransactionDto>
                rowKey={(row) => `${row.transactionId ?? ''}-${row.receiptNumber}-${row.createdAt}`}
                columns={columns}
                dataSource={recent.data ?? []}
                loading={recent.isLoading}
                locale={{ emptyText: t('admin.kassensicherheit.emptyRecent') }}
                pagination={false}
              />
            ),
          },
        ]}
      />
    </Space>
  );
}
