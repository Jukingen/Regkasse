'use client';

import { useQuery } from '@tanstack/react-query';
import { Select, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo, useState } from 'react';

import {
  useAdminTicketRedemptions,
  type AdminTicketRedemptionRow,
} from '@/api/admin/ticket-redemptions';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useI18n } from '@/i18n';

const STATUSES = ['Valid', 'Redeemed', 'Cancelled', 'Expired'] as const;

function stamp(value: string | null | undefined): string {
  return value ? value.slice(0, 19).replace('T', ' ') : '—';
}

export function TicketRedemptionsPage() {
  const { t } = useI18n();
  const [status, setStatus] = useState<string | undefined>();
  const [tenantId, setTenantId] = useState<string | undefined>();

  const tenants = useQuery({
    queryKey: ['admin', 'ticket-redemptions', 'tenants'],
    queryFn: () => listAdminTenants(false),
  });

  const list = useAdminTicketRedemptions({ status, tenantId });
  const rows = list.data?.items ?? [];
  const tenantName = useMemo(() => {
    const map = new Map((tenants.data ?? []).map((tenant) => [tenant.id, tenant.name]));
    return (id: string | undefined) => (id && map.get(id)) || id || '—';
  }, [tenants.data]);

  const columns: ColumnsType<AdminTicketRedemptionRow> = [
    {
      title: t('products.ticketRedemptions.colCode'),
      dataIndex: 'displayCode',
    },
    {
      title: t('products.ticketRedemptions.colTenant'),
      dataIndex: 'tenantId',
      render: (value: string | undefined) => tenantName(value),
    },
    {
      title: t('products.ticketRedemptions.colStatus'),
      dataIndex: 'status',
      render: (value: string | null | undefined) =>
        value && (STATUSES as readonly string[]).includes(value)
          ? t(`products.ticketRedemptions.status.${value}`)
          : value || '—',
    },
    {
      title: t('products.ticketRedemptions.colValidUntil'),
      dataIndex: 'validUntilUtc',
      render: (value: string | null | undefined) => stamp(value),
    },
    {
      title: t('products.ticketRedemptions.colRedeemedAt'),
      dataIndex: 'redeemedAtUtc',
      render: (value: string | null | undefined) => stamp(value),
    },
    {
      title: t('products.ticketRedemptions.colRedeemedBy'),
      dataIndex: 'redeemedByUserId',
      render: (value: string | null | undefined) => value || '—',
    },
  ];

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <AdminPageHeader
        title={t('products.ticketRedemptions.title')}
        subtitle={t('products.ticketRedemptions.subtitle')}
      />
      <Space wrap>
        <Select
          aria-label={t('products.ticketRedemptions.statusFilter')}
          allowClear
          placeholder={t('products.ticketRedemptions.allStatuses')}
          style={{ minWidth: 180 }}
          value={status}
          onChange={(value) => setStatus(value)}
          options={STATUSES.map((item) => ({
            value: item,
            label: t(`products.ticketRedemptions.status.${item}`),
          }))}
        />
        <Select
          aria-label={t('products.ticketRedemptions.tenantFilter')}
          allowClear
          placeholder={t('products.ticketRedemptions.allTenants')}
          style={{ minWidth: 220 }}
          value={tenantId}
          onChange={(value) => setTenantId(value)}
          options={(tenants.data ?? []).map((tenant) => ({ value: tenant.id, label: tenant.name }))}
        />
      </Space>
      {list.isError ? (
        <Typography.Text type="danger">{t('products.ticketRedemptions.loadError')}</Typography.Text>
      ) : null}
      <Table
        rowKey={(row) => row.id}
        loading={list.isLoading}
        dataSource={rows}
        columns={columns}
        pagination={false}
        locale={{ emptyText: t('products.ticketRedemptions.empty') }}
      />
    </Space>
  );
}
