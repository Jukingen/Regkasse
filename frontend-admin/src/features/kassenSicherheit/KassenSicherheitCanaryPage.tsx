'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Input, Select, Space, Switch, Table, Typography } from 'antd';
import { useState } from 'react';

import { setFeatureFlag } from '@/api/manual/featureFlags';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';

import {
  fetchKassenSicherheitRecent,
  fetchKassenSicherheitStatus,
  kassenSicherheitRecentKey,
  kassenSicherheitStatusKey,
  kassenSicherheitTenantsKey,
  postKassenSicherheitExport,
  putKassenSicherheitConfig,
  type KassenSicherheitRecentTransaction,
} from './api';

export function KassenSicherheitCanaryPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [tenantId, setTenantId] = useState<string | undefined>();
  const [draft, setDraft] = useState({ key: '', tssId: '', clientId: '' });

  const tenants = useQuery({
    queryKey: kassenSicherheitTenantsKey,
    queryFn: () => listAdminTenants(false),
  });

  const status = useQuery({
    queryKey: tenantId ? kassenSicherheitStatusKey(tenantId) : ['admin', 'kassensicherheit', 'status', 'none'],
    queryFn: ({ signal }) => fetchKassenSicherheitStatus(tenantId as string, signal),
    enabled: Boolean(tenantId),
  });

  const loadedKey = `${tenantId ?? ''}|${status.data?.deTssId ?? ''}|${status.data?.deClientId ?? ''}`;
  if (draft.key !== loadedKey) {
    setDraft({
      key: loadedKey,
      tssId: status.data?.deTssId ?? '',
      clientId: status.data?.deClientId ?? '',
    });
  }

  const recent = useQuery({
    queryKey: tenantId ? kassenSicherheitRecentKey(tenantId) : ['admin', 'kassensicherheit', 'recent', 'none'],
    queryFn: ({ signal }) => fetchKassenSicherheitRecent(tenantId as string, signal),
    enabled: Boolean(tenantId),
  });

  const saveConfig = useMutation({
    mutationFn: () =>
      putKassenSicherheitConfig({
        tenantId: tenantId as string,
        deTssId: draft.tssId,
        deClientId: draft.clientId,
      }),
    onSuccess: async (data) => {
      if (tenantId) queryClient.setQueryData(kassenSicherheitStatusKey(tenantId), data);
      notify.success(t('kassenSicherheit.configSaved'));
    },
    onError: (err) => {
      notify.apiError(err, { logContext: 'KassenSicherheit.config', fallbackKey: 'kassenSicherheit.loadError' });
    },
  });

  const toggleFlag = useMutation({
    mutationFn: (enabled: boolean) =>
      setFeatureFlag({
        name: 'Fiscal.KassenSicherheitDe',
        enabled,
        tenantId,
      }),
    onSuccess: async (_data, enabled) => {
      if (tenantId) await queryClient.invalidateQueries({ queryKey: kassenSicherheitStatusKey(tenantId) });
      notify.success(t(enabled ? 'kassenSicherheit.flagOn' : 'kassenSicherheit.rolledBack'));
    },
    onError: (err) => {
      notify.apiError(err, { logContext: 'KassenSicherheit.flag', fallbackKey: 'kassenSicherheit.loadError' });
    },
  });

  const exportDsfinvk = useMutation({
    mutationFn: () => postKassenSicherheitExport(tenantId as string),
    onSuccess: (data) => {
      notify.success(data.status === 'PENDING' ? t('kassenSicherheit.exportPending') : data.status);
    },
    onError: (err) => {
      notify.apiError(err, { logContext: 'KassenSicherheit.export', fallbackKey: 'kassenSicherheit.loadError' });
    },
  });

  const columns = [
    { title: t('kassenSicherheit.colTxId'), dataIndex: 'transactionId', key: 'transactionId' },
    { title: t('kassenSicherheit.colBelegnummer'), dataIndex: 'receiptNumber', key: 'receiptNumber' },
    { title: t('kassenSicherheit.colStatus'), dataIndex: 'status', key: 'status' },
    { title: t('kassenSicherheit.colTime'), dataIndex: 'createdAt', key: 'createdAt' },
  ];

  return (
    <>
      <AdminPageHeader title={t('kassenSicherheit.title')} subtitle={t('kassenSicherheit.subtitle')} />
      <Card>
        {tenants.isError || status.isError || recent.isError ? (
          <Alert type="error" showIcon title={t('kassenSicherheit.loadError')} />
        ) : null}
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <label>
            {t('kassenSicherheit.tenantLabel')}
            <Select
              style={{ width: '100%', marginTop: 8 }}
              placeholder={t('kassenSicherheit.tenantLabel')}
              loading={tenants.isLoading}
              value={tenantId}
              options={(tenants.data ?? []).map((row) => ({
                value: row.id,
                label: `${row.name} (${row.slug})`,
              }))}
              onChange={(value: string) => setTenantId(value)}
            />
          </label>
          {!tenantId ? <Typography.Text type="secondary">{t('kassenSicherheit.missingTenant')}</Typography.Text> : null}
          <Space>
            <span>{t('kassenSicherheit.flagLabel')}</span>
            <Switch
              checked={Boolean(status.data?.flagEnabled)}
              disabled={!tenantId}
              loading={toggleFlag.isPending || status.isFetching}
              onChange={(checked) => toggleFlag.mutate(checked)}
            />
            <span>{status.data?.flagEnabled ? t('kassenSicherheit.flagOn') : t('kassenSicherheit.flagOff')}</span>
          </Space>
          <Button disabled={!tenantId || !status.data?.flagEnabled} onClick={() => toggleFlag.mutate(false)}>
            {t('kassenSicherheit.rollback')}
          </Button>
          <Input
            maxLength={64}
            addonBefore={t('kassenSicherheit.tssId')}
            value={draft.tssId}
            disabled={!tenantId}
            onChange={(event) => setDraft((current) => ({ ...current, tssId: event.target.value }))}
          />
          <Input
            maxLength={64}
            addonBefore={t('kassenSicherheit.clientId')}
            value={draft.clientId}
            disabled={!tenantId}
            onChange={(event) => setDraft((current) => ({ ...current, clientId: event.target.value }))}
          />
          <Button type="primary" disabled={!tenantId} loading={saveConfig.isPending} onClick={() => saveConfig.mutate()}>
            {t('kassenSicherheit.saveConfig')}
          </Button>
          <Typography.Title level={5}>{t('kassenSicherheit.recentTitle')}</Typography.Title>
          <Table<KassenSicherheitRecentTransaction>
            rowKey={(row) => `${row.transactionId ?? ''}-${row.receiptNumber}-${row.createdAt}`}
            columns={columns}
            dataSource={recent.data ?? []}
            locale={{ emptyText: t('kassenSicherheit.emptyRecent') }}
            pagination={false}
          />
          <Button disabled={!tenantId} loading={exportDsfinvk.isPending} onClick={() => exportDsfinvk.mutate()}>
            {t('kassenSicherheit.exportDsfinvk')}
          </Button>
        </Space>
      </Card>
    </>
  );
}
