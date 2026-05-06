'use client';

import React, { useMemo, useState } from 'react';
import { Alert, Button, Card, Input, Space, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { PlusOutlined, SearchOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { useAdminVouchersList } from '@/api/admin/vouchers';
import type { AdminVoucherListItemDto } from '@/api/admin/vouchers';
import { formatCurrency, formatDateTime } from '@/i18n/formatting';

function statusColor(status: string): string {
  switch (status) {
    case 'Active':
      return 'green';
    case 'PartiallyRedeemed':
      return 'blue';
    case 'Redeemed':
      return 'default';
    case 'Cancelled':
      return 'red';
    case 'Expired':
      return 'orange';
    default:
      return 'default';
  }
}

function creatorFallback(userId?: string | null): string {
  if (!userId) return '—';
  const trimmed = userId.trim();
  if (!trimmed) return '—';
  return trimmed.length > 12 ? `${trimmed.slice(0, 8)}…` : trimmed;
}

function formatCreator(value: AdminVoucherListItemDto): string {
  const parts = [
    value.createdByDisplayName?.trim(),
    value.createdByEmail?.trim(),
  ].filter((x): x is string => !!x);
  const roleText = (value.createdByRoles ?? []).filter(Boolean).join(', ');
  if (parts.length > 0 && roleText) return `${parts.join(' · ')} (${roleText})`;
  if (parts.length > 0) return parts.join(' · ');
  if (roleText) return `${creatorFallback(value.createdByUserId)} (${roleText})`;
  return creatorFallback(value.createdByUserId);
}

export default function AdminVouchersListPage() {
  const { t, formatLocale } = useI18n();
  const { hasPermission } = usePermissions();
  const canRead = hasPermission(PERMISSIONS.VOUCHER_READ);
  const canCreate = hasPermission(PERMISSIONS.VOUCHER_CREATE);

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [qInput, setQInput] = useState('');
  const [q, setQ] = useState('');

  const listQuery = useAdminVouchersList(
    { page, pageSize, q },
    { enabled: canRead }
  );

  const columns: ColumnsType<AdminVoucherListItemDto> = useMemo(
    () => [
      { title: t('vouchers.list.columns.maskedCode'), dataIndex: 'maskedCode', key: 'maskedCode' },
      {
        title: t('vouchers.list.columns.initialAmount'),
        dataIndex: 'initialAmount',
        key: 'initialAmount',
        render: (v: number, r) => formatCurrency(v, formatLocale, { currency: r.currency || 'EUR' }),
      },
      {
        title: t('vouchers.list.columns.remainingAmount'),
        dataIndex: 'remainingAmount',
        key: 'remainingAmount',
        render: (v: number, r) => formatCurrency(v, formatLocale, { currency: r.currency || 'EUR' }),
      },
      { title: t('vouchers.list.columns.currency'), dataIndex: 'currency', key: 'currency', width: 90 },
      {
        title: t('vouchers.list.columns.status'),
        dataIndex: 'status',
        key: 'status',
        render: (s: string) => {
          const key = `vouchers.status.${s}`;
          const lbl = t(key);
          return <Tag color={statusColor(s)}>{lbl === key ? s : lbl}</Tag>;
        },
      },
      {
        title: t('vouchers.list.columns.validFrom'),
        dataIndex: 'validFromUtc',
        key: 'validFromUtc',
        render: (iso: string) => formatDateTime(iso, formatLocale),
      },
      {
        title: t('vouchers.list.columns.expiresAt'),
        dataIndex: 'expiresAtUtc',
        key: 'expiresAtUtc',
        render: (iso: string) => formatDateTime(iso, formatLocale),
      },
      {
        title: t('vouchers.list.columns.createdBy'),
        dataIndex: 'createdByUserId',
        key: 'createdByUserId',
        ellipsis: true,
        render: (_: string, row) => formatCreator(row),
      },
      {
        title: t('vouchers.list.columns.createdAt'),
        dataIndex: 'createdAtUtc',
        key: 'createdAtUtc',
        render: (iso: string) => formatDateTime(iso, formatLocale),
      },
      {
        title: t('vouchers.list.columns.actions'),
        key: 'actions',
        fixed: 'right',
        width: 120,
        render: (_, r) => (
          <Link href={`/vouchers/${r.id}`}>{t('vouchers.list.open')}</Link>
        ),
      },
    ],
    [t, formatLocale]
  );

  if (!canRead) {
    return (
      <AdminPageShell>
        <Alert type="error" message={t('vouchers.list.permissionDenied')} showIcon />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('vouchers.list.heading')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('vouchers.title'), href: '/vouchers' }]}
        actions={
          canCreate ? (
            <Link href="/vouchers/new">
              <Button type="primary" icon={<PlusOutlined />}>
                {t('vouchers.list.create')}
              </Button>
            </Link>
          ) : undefined
        }
      />
      {listQuery.isError ? (
        <Alert type="error" message={t('vouchers.errors.loadFailed')} showIcon style={{ marginBottom: 16 }} />
      ) : null}
      <Card>
        <Space wrap style={{ marginBottom: 16 }}>
          <Input
            allowClear
            placeholder={t('vouchers.list.searchPlaceholder')}
            value={qInput}
            onChange={(e) => setQInput(e.target.value)}
            onPressEnter={() => {
              setQ(qInput.trim());
              setPage(1);
            }}
            style={{ width: 280 }}
          />
          <Button
            type="default"
            icon={<SearchOutlined />}
            onClick={() => {
              setQ(qInput.trim());
              setPage(1);
            }}
          >
            {t('vouchers.list.searchButton')}
          </Button>
        </Space>
        <Table<AdminVoucherListItemDto>
          rowKey="id"
          loading={listQuery.isLoading}
          dataSource={listQuery.data?.items ?? []}
          columns={columns}
          scroll={{ x: true }}
          locale={{ emptyText: t('vouchers.list.empty') }}
          pagination={{
            current: page,
            pageSize,
            total: listQuery.data?.totalCount ?? 0,
            showSizeChanger: true,
            pageSizeOptions: [10, 20, 50],
            onChange: (p, ps) => {
              setPage(p);
              setPageSize(ps);
            },
          }}
        />
      </Card>
    </AdminPageShell>
  );
}
