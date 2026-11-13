'use client';

import { Button, Card, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useState } from 'react';

import { ConfirmDialog } from '@/components/ConfirmDialog';
import { EmptyState } from '@/components/EmptyState';
import { StatusBadge } from '@/components/StatusBadge';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import type { ActiveSession } from '@/features/auth/api/sessionsApi';
import { useSessions } from '@/features/auth/hooks/useSessions';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import dayjs from '@/lib/dayjs';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

type PendingConfirm = { kind: 'revoke'; session: ActiveSession } | { kind: 'revokeOthers' } | null;

export default function ActiveSessionsPage() {
  const { message } = useAntdApp();
  const { t } = useI18n();
  const { sessions, isLoading, isFetching, refetch, revoke, revokeOthers, isRevoking } =
    useSessions();
  const [pendingConfirm, setPendingConfirm] = useState<PendingConfirm>(null);
  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.settingsHub'), href: '/settings' },
    { title: t('common.auth.sessions.title') },
  ];

  const handleRevokeClick = (session: ActiveSession) => {
    if (session.isCurrent) {
      message.warning(t('common.auth.sessions.cannotTerminateCurrent'));
      return;
    }
    setPendingConfirm({ kind: 'revoke', session });
  };

  const handleConfirm = async () => {
    if (!pendingConfirm) return;
    try {
      if (pendingConfirm.kind === 'revoke') {
        await revoke(pendingConfirm.session.id);
        message.success(t('common.auth.sessions.terminated'));
      } else {
        const count = await revokeOthers();
        message.success(t('common.auth.sessions.terminatedOthers', { count: String(count) }));
      }
      setPendingConfirm(null);
    } catch {
      message.error(t('common.auth.sessions.terminateFailed'));
    }
  };

  const columns: ColumnsType<ActiveSession> = [
    {
      title: t('common.auth.sessions.colDevice'),
      key: 'deviceName',
      render: (_, record) => record.deviceName?.trim() || t('common.auth.sessions.unknownDevice'),
    },
    {
      title: t('common.auth.sessions.colBrowserOs'),
      key: 'browserOs',
      render: (_, record) => (
        <span>
          {record.browser || '—'} / {record.os || '—'}
        </span>
      ),
    },
    {
      title: t('common.auth.sessions.colIp'),
      dataIndex: 'ipAddress',
      key: 'ipAddress',
      render: (value: string | null | undefined) => value || '—',
    },
    {
      title: t('common.auth.sessions.lastActivity'),
      dataIndex: 'lastActivityAtUtc',
      key: 'lastActivityAtUtc',
      render: (value: string) => (value ? dayjs(value).fromNow() : '—'),
    },
    {
      title: t('common.auth.sessions.colStatus'),
      key: 'status',
      render: (_, record) => (
        <Space wrap>
          {record.isCurrent ? (
            <StatusBadge status="info" label={t('common.auth.sessions.thisDevice')} />
          ) : null}
          <StatusBadge
            status={record.isActive ? 'active' : 'inactive'}
            label={
              record.isActive
                ? t('common.auth.sessions.statusActive')
                : t('common.auth.sessions.statusInactive')
            }
          />
        </Space>
      ),
    },
    {
      title: t('common.auth.sessions.colAction'),
      key: 'action',
      render: (_, record) =>
        record.isCurrent ? null : (
          <Button
            danger
            size="small"
            loading={isRevoking}
            onClick={() => handleRevokeClick(record)}
          >
            {t('common.auth.sessions.endSession')}
          </Button>
        ),
    },
  ];

  const hasOtherSessions = sessions.some((s) => !s.isCurrent);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader title={t('common.auth.sessions.title')} breadcrumbs={breadcrumbs} />

      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {t('common.auth.sessions.description')}
      </Typography.Paragraph>

      <Card
        extra={
          <Space wrap>
            <Button onClick={() => void refetch()} loading={isFetching}>
              {t('common.buttons.refresh')}
            </Button>
            <Button
              danger
              disabled={!hasOtherSessions}
              loading={isRevoking}
              onClick={() => setPendingConfirm({ kind: 'revokeOthers' })}
            >
              {t('common.auth.sessions.terminateAllOthers')}
            </Button>
          </Space>
        }
      >
        <Table<ActiveSession>
          dataSource={sessions}
          columns={columns}
          rowKey="id"
          loading={isLoading}
          pagination={false}
          locale={{
            emptyText: (
              <EmptyState
                title={t('common.auth.sessions.empty')}
                description={t('common.auth.sessions.description')}
              />
            ),
          }}
        />
      </Card>

      <ConfirmDialog
        open={pendingConfirm?.kind === 'revoke'}
        title={t('common.auth.sessions.revokeConfirmTitle')}
        message={t('common.auth.sessions.revokeConfirmContent')}
        type="danger"
        confirmText={t('common.auth.sessions.endSession')}
        loading={isRevoking}
        onConfirm={() => void handleConfirm()}
        onCancel={() => setPendingConfirm(null)}
      />
      <ConfirmDialog
        open={pendingConfirm?.kind === 'revokeOthers'}
        title={t('common.auth.sessions.terminateAllOthers')}
        message={t('common.auth.sessions.revokeOthersConfirmContent')}
        type="danger"
        confirmText={t('common.auth.sessions.terminateAllOthers')}
        loading={isRevoking}
        onConfirm={() => void handleConfirm()}
        onCancel={() => setPendingConfirm(null)}
      />
    </div>
  );
}
