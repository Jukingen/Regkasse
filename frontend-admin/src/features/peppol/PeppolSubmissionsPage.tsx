'use client';

import { useQuery } from '@tanstack/react-query';
import { Card, Select, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useMemo, useState } from 'react';

import {
  useGetApiAdminPeppolSubmissions,
  useGetApiAdminPeppolSubmissionsId,
} from '@/api/generated/admin/admin';
import type { PeppolSubmissionRowDto } from '@/api/generated/model';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useI18n } from '@/i18n';

import { failureHintSuffix } from './failureHint';

const STATUSES = ['Queued', 'Sent', 'Ack', 'Failed'] as const;

function stamp(value: string | null | undefined): string {
  return value ? value.slice(0, 19).replace('T', ' ') : '—';
}

export function PeppolSubmissionsPage() {
  const { t } = useI18n();
  const [status, setStatus] = useState<string | undefined>();
  const [tenantId, setTenantId] = useState<string | undefined>();
  const [selectedId, setSelectedId] = useState<string | undefined>();

  const tenants = useQuery({
    queryKey: ['admin', 'peppol', 'tenants'],
    queryFn: () => listAdminTenants(false),
  });

  const list = useGetApiAdminPeppolSubmissions({
    tenantId,
    status,
    limit: 50,
    offset: 0,
  });

  const rows = list.data?.items ?? [];
  const activeId = selectedId ?? rows[0]?.id;
  const detail = useGetApiAdminPeppolSubmissionsId(activeId ?? '', {
    query: { enabled: Boolean(activeId) },
  });
  const tenantName = useMemo(() => {
    const map = new Map((tenants.data ?? []).map((tenant) => [tenant.id, tenant.name]));
    return (id: string | undefined) => (id && map.get(id)) || id || '—';
  }, [tenants.data]);

  const selected = rows.find((row) => row.id === activeId) ?? detail.data;
  const invoiceHref =
    detail.data?.id && detail.data.id === activeId && detail.data.invoiceHref
      ? detail.data.invoiceHref
      : null;
  const hint = selected
    ? t(`peppol.submissions.failure.${failureHintSuffix(selected.failureReason)}`)
    : t('peppol.submissions.empty');

  const columns: ColumnsType<PeppolSubmissionRowDto> = [
    {
      title: t('peppol.submissions.colStatus'),
      dataIndex: 'status',
      render: (value: string | null | undefined) =>
        value && (STATUSES as readonly string[]).includes(value)
          ? t(`peppol.submissions.status.${value}`)
          : value || '—',
    },
    {
      title: t('peppol.submissions.colTenant'),
      dataIndex: 'tenantId',
      render: (value: string | undefined) => tenantName(value),
    },
    {
      title: t('peppol.submissions.colInvoice'),
      dataIndex: 'invoiceId',
      render: (value: string | undefined, row) =>
        value ? (
          <Link href={row.id === activeId && invoiceHref ? invoiceHref : `/invoices?invoiceId=${value}`}>
            {value}
          </Link>
        ) : (
          '—'
        ),
    },
    {
      title: t('peppol.submissions.colAttempted'),
      dataIndex: 'attemptedAtUtc',
      render: (value: string | null | undefined) => stamp(value),
    },
    {
      title: t('peppol.submissions.colAcked'),
      dataIndex: 'ackedAtUtc',
      render: (value: string | null | undefined) => stamp(value),
    },
    {
      title: t('peppol.submissions.colProviderMessageId'),
      dataIndex: 'providerMessageId',
      render: (value: string | null | undefined) => value || '—',
    },
    {
      title: t('peppol.submissions.colFailure'),
      dataIndex: 'failureReason',
      render: (value: string | null | undefined) => value || '—',
    },
  ];

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <AdminPageHeader title={t('peppol.submissions.title')} subtitle={t('peppol.submissions.subtitle')} />
      <Space wrap>
        <Select
          aria-label={t('peppol.submissions.statusFilter')}
          allowClear
          placeholder={t('peppol.submissions.allStatuses')}
          style={{ minWidth: 180 }}
          value={status}
          onChange={(value) => setStatus(value)}
          options={STATUSES.map((item) => ({
            value: item,
            label: t(`peppol.submissions.status.${item}`),
            title: item,
          }))}
        />
        <Select
          aria-label={t('peppol.submissions.tenantFilter')}
          allowClear
          placeholder={t('peppol.submissions.allTenants')}
          style={{ minWidth: 220 }}
          value={tenantId}
          onChange={(value) => setTenantId(value)}
          options={(tenants.data ?? []).map((tenant) => ({ value: tenant.id, label: tenant.name }))}
        />
      </Space>
      {list.isError ? <Typography.Text type="danger">{t('peppol.submissions.loadError')}</Typography.Text> : null}
      <Table
        rowKey={(row) => row.id ?? ''}
        loading={list.isLoading}
        dataSource={rows}
        columns={columns}
        pagination={false}
        locale={{ emptyText: t('peppol.submissions.empty') }}
        onRow={(row) => ({
          onClick: () => setSelectedId(row.id),
        })}
      />
      <Card title={t('peppol.submissions.guidanceTitle')}>
        <Typography.Paragraph style={{ marginBottom: 0 }}>{hint}</Typography.Paragraph>
        {selected?.failureReason ? (
          <Typography.Text type="secondary">{selected.failureReason}</Typography.Text>
        ) : null}
      </Card>
    </Space>
  );
}
