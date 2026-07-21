'use client';

import { Alert, Button, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useState } from 'react';

import type {
  DigitalServiceRequest,
  DigitalServiceRequestStatus,
} from '@/features/digital-services/api/digitalServiceRequestsApi';
import type { DigitalServiceType } from '@/features/digital-services/api/tenantDigitalServicesApi';
import {
  type DigitalServiceRequestListFilter,
  useApproveDigitalServiceRequest,
  useDigitalServiceRequests,
  useRejectDigitalServiceRequest,
} from '@/features/digital-services/hooks/useDigitalServiceRequests';
import { useAntdApp } from '@/hooks/useAntdApp';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

const { Paragraph, Title } = Typography;

type DigitalServiceRequestsPanelProps = {
  /** When true, show status filter and status column (dedicated requests page). */
  showStatusFilter?: boolean;
  /** Optional link under the title (e.g. open full requests page from management hub). */
  viewAllHref?: string;
  titleLevel?: 4 | 5;
};

function statusTagColor(status: DigitalServiceRequestStatus): string {
  switch (status) {
    case 'Approved':
      return 'success';
    case 'Rejected':
      return 'error';
    case 'Cancelled':
      return 'default';
    default:
      return 'processing';
  }
}

export function DigitalServiceRequestsPanel({
  showStatusFilter = false,
  viewAllHref,
  titleLevel = 5,
}: DigitalServiceRequestsPanelProps) {
  const { t, formatLocale } = useI18n();
  const { message } = useAntdApp();
  const { hasPermission, isSuperAdmin } = usePermissions();
  const canManage =
    isSuperAdmin ||
    hasPermission(PERMISSIONS.DIGITAL_MANAGE) ||
    hasPermission(PERMISSIONS.SYSTEM_CRITICAL);

  const [statusFilter, setStatusFilter] = useState<DigitalServiceRequestListFilter>('Pending');
  const { data, isLoading, isError } = useDigitalServiceRequests(statusFilter);
  const approveMutation = useApproveDigitalServiceRequest();
  const rejectMutation = useRejectDigitalServiceRequest();

  const formatDate = (iso: string) => {
    try {
      return new Intl.DateTimeFormat(formatLocale, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(iso));
    } catch {
      return iso;
    }
  };

  const serviceLabel = (serviceType: DigitalServiceType) =>
    serviceType === 'website'
      ? t('superadmin.digital.columns.website')
      : t('superadmin.digital.columns.app');

  const statusLabel = (status: DigitalServiceRequestStatus) => {
    switch (status) {
      case 'Approved':
        return t('superadmin.digital.requestStatus.approved');
      case 'Rejected':
        return t('superadmin.digital.requestStatus.rejected');
      case 'Cancelled':
        return t('superadmin.digital.requestStatus.cancelled');
      default:
        return t('superadmin.digital.requestStatus.pending');
    }
  };

  const resolveRequest = async (id: string, action: 'approve' | 'reject') => {
    try {
      if (action === 'approve') {
        await approveMutation.mutateAsync({ id });
        message.success(t('superadmin.digital.requestApproved'));
      } else {
        await rejectMutation.mutateAsync({ id });
        message.success(t('superadmin.digital.requestRejected'));
      }
    } catch {
      message.error(t('superadmin.digital.requestResolveFailed'));
    }
  };

  const columns: ColumnsType<DigitalServiceRequest> = [
    {
      title: t('superadmin.digital.requestColumns.tenant'),
      key: 'tenant',
      render: (_, row) => (
        <Space orientation="vertical" size={0}>
          <Link href={`/admin/tenants/${row.tenantId}/digital`}>
            {row.tenantName ?? row.tenantId}
          </Link>
          {row.tenantSlug ? (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {row.tenantSlug}
            </Typography.Text>
          ) : null}
        </Space>
      ),
    },
    {
      title: t('superadmin.digital.requestColumns.service'),
      key: 'service',
      render: (_, row) => serviceLabel(row.serviceType),
    },
    {
      title: t('superadmin.digital.requestColumns.requestedAt'),
      key: 'requestedAt',
      render: (_, row) => formatDate(row.requestedAt),
    },
    {
      title: t('superadmin.digital.requestColumns.note'),
      dataIndex: 'note',
      key: 'note',
      render: (note: string | null) => note || '—',
    },
    ...(showStatusFilter
      ? ([
          {
            title: t('superadmin.digital.requestColumns.status'),
            key: 'status',
            render: (_: unknown, row: DigitalServiceRequest) => (
              <Tag color={statusTagColor(row.status)}>{statusLabel(row.status)}</Tag>
            ),
          },
        ] as ColumnsType<DigitalServiceRequest>)
      : []),
    {
      title: t('superadmin.digital.requestColumns.actions'),
      key: 'actions',
      render: (_, row) =>
        row.status === 'Pending' ? (
          <Space>
            <Button
              type="primary"
              size="small"
              loading={approveMutation.isPending}
              onClick={() => void resolveRequest(row.id, 'approve')}
            >
              {t('superadmin.digital.requestApprove')}
            </Button>
            <Button
              danger
              size="small"
              loading={rejectMutation.isPending}
              onClick={() => void resolveRequest(row.id, 'reject')}
            >
              {t('superadmin.digital.requestReject')}
            </Button>
          </Space>
        ) : (
          <Typography.Text type="secondary">—</Typography.Text>
        ),
    },
  ];

  if (!canManage) {
    return <Alert type="warning" showIcon message={t('superadmin.digital.accessDenied')} />;
  }

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <Space style={{ width: '100%', justifyContent: 'space-between' }} wrap align="center">
        <Title level={titleLevel} style={{ margin: 0 }}>
          {t('superadmin.digital.requestsTitle')}
        </Title>
        <Space wrap>
          {showStatusFilter ? (
            <Select<DigitalServiceRequestListFilter>
              value={statusFilter}
              onChange={setStatusFilter}
              style={{ minWidth: 160 }}
              options={[
                { value: 'Pending', label: t('superadmin.digital.requestFilter.pending') },
                { value: 'Approved', label: t('superadmin.digital.requestFilter.approved') },
                { value: 'Rejected', label: t('superadmin.digital.requestFilter.rejected') },
                { value: 'all', label: t('superadmin.digital.requestFilter.all') },
              ]}
            />
          ) : null}
          {viewAllHref ? (
            <Link href={viewAllHref}>
              <Button type="link">{t('superadmin.digital.requestsViewAll')}</Button>
            </Link>
          ) : null}
        </Space>
      </Space>

      <Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {showStatusFilter
          ? t('superadmin.digital.requestsPageSubtitle')
          : t('superadmin.digital.requestsHint')}
      </Paragraph>

      {isError ? (
        <Alert type="error" showIcon message={t('superadmin.digital.requestsLoadFailed')} />
      ) : (
        <Table<DigitalServiceRequest>
          loading={isLoading}
          dataSource={data ?? []}
          columns={columns}
          rowKey="id"
          pagination={showStatusFilter ? { pageSize: 20 } : false}
          locale={{ emptyText: t('superadmin.digital.requestsEmpty') }}
        />
      )}
    </Space>
  );
}
