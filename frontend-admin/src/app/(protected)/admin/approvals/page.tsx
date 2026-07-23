'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Modal, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { dateColumnRender } from '@/components/DateColumn';
import {
  approveAction,
  getPendingApprovals,
  rejectAction,
  type ApprovalRequestDto,
} from '@/features/admin/api/approvals';
import { ApprovalHistoryPanel } from '@/features/admin/components/ApprovalHistoryPanel';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

const PENDING_APPROVALS_QUERY_KEY = ['admin', 'approvals', 'pending'] as const;

export default function ApprovalsPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const { isSuperAdmin } = usePermissions();
  const queryClient = useQueryClient();
  const [detail, setDetail] = useState<ApprovalRequestDto | null>(null);

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.administration'), href: '/admin' },
    { title: t('common.approvals.pageTitle') },
  ];

  const { data: approvals = [], isLoading, isError, refetch } = useQuery({
    queryKey: PENDING_APPROVALS_QUERY_KEY,
    queryFn: getPendingApprovals,
    enabled: isSuperAdmin,
  });

  const approveMutation = useMutation({
    mutationFn: (id: string) => approveAction(id),
    onSuccess: async () => {
      notify.successKey('common.approvals.approved');
      await queryClient.invalidateQueries({ queryKey: ['admin', 'approvals'] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'ApprovalsPage.approve',
        fallbackKey: 'common.approvals.actionFailed',
      });
    },
  });

  const rejectMutation = useMutation({
    mutationFn: (id: string) => rejectAction(id),
    onSuccess: async () => {
      notify.successKey('common.approvals.rejected');
      await queryClient.invalidateQueries({ queryKey: ['admin', 'approvals'] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'ApprovalsPage.reject',
        fallbackKey: 'common.approvals.actionFailed',
      });
    },
  });

  if (!isSuperAdmin) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('common.approvals.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert type="warning" showIcon title={t('common.approvals.accessDenied')} />
      </AdminPageShell>
    );
  }

  const columns: ColumnsType<ApprovalRequestDto> = [
    {
      title: t('common.approvals.columns.action'),
      dataIndex: 'actionType',
      key: 'actionType',
      render: (action: string) => <Tag color="orange">{action}</Tag>,
    },
    {
      title: t('common.approvals.columns.requestedBy'),
      key: 'requestedBy',
      ellipsis: true,
      render: (_: unknown, row) =>
        row.requestedByEmail ||
        row.requestedByDisplayName ||
        row.requestedBy ||
        t('common.approvals.unknownUser'),
    },
    {
      title: t('common.approvals.columns.tenant'),
      key: 'tenant',
      ellipsis: true,
      render: (_: unknown, row) => row.tenantName || row.tenantSlug || row.tenantId || '—',
    },
    {
      title: t('common.approvals.columns.requestedAt'),
      dataIndex: 'requestedAt',
      key: 'requestedAt',
      render: dateColumnRender('datetimeSeconds'),
    },
    {
      title: t('common.approvals.columns.expiresAt'),
      dataIndex: 'expiresAt',
      key: 'expiresAt',
      render: dateColumnRender('datetimeSeconds'),
    },
    {
      title: t('common.approvals.columns.details'),
      key: 'details',
      render: (_: unknown, record) => (
        <Button size="small" onClick={() => setDetail(record)}>
          {t('common.approvals.viewDetails')}
        </Button>
      ),
    },
    {
      title: t('common.approvals.columns.actions'),
      key: 'actions',
      render: (_: unknown, record) => {
        const busy =
          (approveMutation.isPending && approveMutation.variables === record.id) ||
          (rejectMutation.isPending && rejectMutation.variables === record.id);
        return (
          <Space>
            <Button
              type="primary"
              size="small"
              loading={busy && approveMutation.isPending}
              disabled={busy}
              onClick={() => {
                modal.confirm({
                  title: t('common.approvals.confirmApproveTitle'),
                  content: t('common.approvals.confirmApproveBody', { action: record.actionType }),
                  okText: t('common.approvals.approve'),
                  okButtonProps: { danger: false },
                  onOk: () => approveMutation.mutateAsync(record.id),
                });
              }}
            >
              {t('common.approvals.approve')}
            </Button>
            <Button
              danger
              size="small"
              loading={busy && rejectMutation.isPending}
              disabled={busy}
              onClick={() => {
                modal.confirm({
                  title: t('common.approvals.confirmRejectTitle'),
                  content: t('common.approvals.confirmRejectBody', { action: record.actionType }),
                  okText: t('common.approvals.reject'),
                  okButtonProps: { danger: true },
                  onOk: () => rejectMutation.mutateAsync(record.id),
                });
              }}
            >
              {t('common.approvals.reject')}
            </Button>
          </Space>
        );
      },
    },
  ];

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('common.approvals.pageTitle')}
        breadcrumbs={breadcrumbs}
        extra={
          <Button onClick={() => void refetch()} loading={isLoading}>
            {t('common.buttons.refresh')}
          </Button>
        }
      />
      {isError ? (
        <Alert
          type="error"
          showIcon
          title={t('common.approvals.loadFailed')}
          style={{ marginBottom: 16 }}
        />
      ) : null}
      <Card title={t('common.approvals.pendingTitle')}>
        <Table
          dataSource={approvals}
          rowKey="id"
          loading={isLoading}
          columns={columns}
          pagination={{ pageSize: 20 }}
          locale={{ emptyText: t('common.approvals.empty') }}
        />
      </Card>

      <div style={{ marginTop: 24 }}>
        <ApprovalHistoryPanel enabled={isSuperAdmin} />
      </div>

      <Modal
        open={detail != null}
        title={t('common.approvals.detailTitle')}
        onCancel={() => setDetail(null)}
        footer={
          <Button onClick={() => setDetail(null)}>{t('common.buttons.close')}</Button>
        }
        destroyOnHidden
        width={640}
      >
        {detail ? (
          <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
            <div>
              <Typography.Text type="secondary">{t('common.approvals.columns.action')}</Typography.Text>
              <div>
                <Tag color="orange">{detail.actionType}</Tag>
              </div>
            </div>
            <div>
              <Typography.Text type="secondary">{t('common.approvals.columns.reason')}</Typography.Text>
              <Typography.Paragraph>{detail.reason || '—'}</Typography.Paragraph>
            </div>
            <div>
              <Typography.Text type="secondary">{t('common.approvals.columns.pathHint')}</Typography.Text>
              <Typography.Paragraph code>{detail.pathHint || '—'}</Typography.Paragraph>
            </div>
            <div>
              <Typography.Text type="secondary">{t('common.approvals.columns.payload')}</Typography.Text>
              <Typography.Paragraph>
                <pre style={{ whiteSpace: 'pre-wrap', margin: 0, maxHeight: 240, overflow: 'auto' }}>
                  {detail.payload || '—'}
                </pre>
              </Typography.Paragraph>
            </div>
          </Space>
        ) : null}
      </Modal>
    </AdminPageShell>
  );
}
