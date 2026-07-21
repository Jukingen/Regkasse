'use client';

import { LoginOutlined } from '@ant-design/icons';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useCallback, useMemo, useState } from 'react';

import {
  type AdminTenantListItem,
  applyTenantImpersonationSession,
  impersonateAdminTenant,
  listAdminTenants,
} from '@/features/super-admin/api/adminTenants';
import { ImpersonationRedirectOverlay } from '@/features/super-admin/components/ImpersonationRedirectOverlay';
import {
  buildTenantSelectorLabel,
  getTenantSelectorStatus,
} from '@/features/super-admin/utils/tenantSelectorLabel';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

const TENANT_LIST_QUERY_KEY = ['admin', 'tenants', false] as const;

function statusColor(status: string): string {
  if (status === 'active') return 'green';
  if (status === 'suspended') return 'orange';
  if (status === 'deleted') return 'red';
  return 'default';
}

export function SuperAdminTenantSelector() {
  const { message } = useAntdApp();

  const { t } = useI18n();
  const [selectedTenantId, setSelectedTenantId] = useState<string | null>(null);
  const [impersonationRedirecting, setImpersonationRedirecting] = useState(false);

  const tenantsQuery = useQuery({
    queryKey: TENANT_LIST_QUERY_KEY,
    queryFn: () => listAdminTenants(false),
  });

  const activeTenants = useMemo(
    () => (tenantsQuery.data ?? []).filter((row) => row.status === 'active' && row.isActive),
    [tenantsQuery.data]
  );

  const impersonateMutation = useMutation({
    mutationFn: (tenantId: string) => impersonateAdminTenant(tenantId),
    onSuccess: (res) => {
      setImpersonationRedirecting(true);
      applyTenantImpersonationSession(res);
    },
    onError: () => message.error(t('tenants.messages.impersonationFailed')),
  });

  const runImpersonation = useCallback(
    (tenantId: string) => {
      if (!tenantId) return;
      impersonateMutation.mutate(tenantId);
    },
    [impersonateMutation]
  );

  const selectOptions = useMemo(
    () =>
      activeTenants.map((row) => ({
        value: row.id,
        label: buildTenantSelectorLabel(row, t),
      })),
    [activeTenants, t]
  );

  const columns: ColumnsType<AdminTenantListItem> = useMemo(
    () => [
      { title: t('tenants.columns.name'), dataIndex: 'name', key: 'name' },
      { title: t('tenants.columns.slug'), dataIndex: 'slug', key: 'slug' },
      {
        title: t('superadmin.selector.adminColumn'),
        key: 'admin',
        render: (_, row) => {
          const status = getTenantSelectorStatus(row, t);
          const color =
            status.kind === 'admin' ? 'success' : status.kind === 'demo' ? 'warning' : 'error';
          return <Tag color={color}>{status.suffix}</Tag>;
        },
      },
      {
        title: t('tenants.columns.status'),
        dataIndex: 'status',
        key: 'status',
        render: (status: string) => <Tag color={statusColor(status)}>{status}</Tag>,
      },
      {
        title: t('tenants.columns.actions'),
        key: 'actions',
        render: (_, row) => (
          <Button
            size="small"
            type="primary"
            icon={<LoginOutlined />}
            loading={impersonateMutation.isPending}
            disabled={row.status !== 'active' || !row.isActive}
            onClick={() => runImpersonation(row.id)}
          >
            {t('superadmin.impersonate')}
          </Button>
        ),
      },
    ],
    [t, impersonateMutation.isPending, runImpersonation]
  );

  return (
    <>
      {impersonationRedirecting ? <ImpersonationRedirectOverlay /> : null}
      <Space orientation="vertical" size={16} style={{ width: '100%' }}>
        <Alert
          type="warning"
          showIcon
          title={t('superadmin.noTenant.banner')}
          description={t('superadmin.noTenant.message')}
        />

        <Card title={t('superadmin.noTenant.title')} size="small">
          <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
            {t('superadmin.selectTenant')}
          </Typography.Paragraph>

          <Space wrap style={{ marginBottom: 16 }}>
            <Select
              showSearch
              allowClear
              placeholder={t('superadmin.selectorPlaceholder')}
              style={{ minWidth: 420 }}
              popupMatchSelectWidth={520}
              loading={tenantsQuery.isLoading}
              options={selectOptions}
              value={selectedTenantId}
              onChange={setSelectedTenantId}
              optionFilterProp="label"
              aria-label={t('superadmin.selectTenant')}
            />
            <Button
              type="primary"
              icon={<LoginOutlined />}
              disabled={!selectedTenantId}
              loading={impersonateMutation.isPending}
              onClick={() => selectedTenantId && runImpersonation(selectedTenantId)}
            >
              {t('superadmin.impersonate')}
            </Button>
            <Link href="/admin/tenants">
              <Button>{t('superadmin.openTenantManagement')}</Button>
            </Link>
          </Space>

          {tenantsQuery.isError ? (
            <Alert type="error" showIcon title={t('superadmin.loadFailed')} />
          ) : (
            <Table
              size="small"
              rowKey="id"
              loading={tenantsQuery.isLoading}
              dataSource={activeTenants}
              columns={columns}
              pagination={{ pageSize: 8, showSizeChanger: false }}
            />
          )}
        </Card>
      </Space>
    </>
  );
}
