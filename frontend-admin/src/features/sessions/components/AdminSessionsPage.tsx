'use client';

import { Button, Card, Input, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useMemo, useState } from 'react';

import type { AdminActiveSession } from '@/api/manual/adminSessions';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { EmptyState } from '@/components/EmptyState';
import { StatusBadge } from '@/components/StatusBadge';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { useAdminSessions } from '@/features/sessions/hooks/useAdminSessions';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import dayjs from '@/lib/dayjs';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';

type PendingConfirm =
  | { kind: 'revoke'; session: AdminActiveSession }
  | { kind: 'revokeAll' }
  | null;

function sessionUserLabel(session: AdminActiveSession): string {
  return (
    session.displayName?.trim() ||
    session.userName?.trim() ||
    session.email?.trim() ||
    session.userId
  );
}

export function AdminSessionsPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { isSuperAdmin } = usePermissions();
  const { sessions, isLoading, isFetching, refetch, terminateOne, terminateAll } =
    useAdminSessions(isSuperAdmin);
  const [search, setSearch] = useState('');
  const [pendingConfirm, setPendingConfirm] = useState<PendingConfirm>(null);

  const breadcrumbs = buildPlatformAdminBreadcrumbs(t, 'administration', {
    title: t('nav.sessions'),
  });

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return sessions;
    return sessions.filter((s) => {
      const hay = [s.displayName, s.userName, s.email, s.userId, s.role, s.ipAddress, s.deviceName]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();
      return hay.includes(q);
    });
  }, [sessions, search]);

  const isMutating = terminateOne.isPending || terminateAll.isPending;

  const handleConfirm = async () => {
    if (!pendingConfirm) return;
    try {
      if (pendingConfirm.kind === 'revoke') {
        await terminateOne.mutateAsync(pendingConfirm.session.id);
        notify.successKey('users.sessions.terminated');
      } else {
        const result = await terminateAll.mutateAsync();
        notify.success(
          t('users.sessions.terminatedCount', { count: String(result.terminatedCount) }),
        );
      }
      setPendingConfirm(null);
    } catch {
      notify.errorKey('users.sessions.terminateFailed');
    }
  };

  const columns: ColumnsType<AdminActiveSession> = [
    {
      title: t('users.sessions.colUser'),
      key: 'user',
      render: (_, record) => (
        <Link href={`/admin/users/${encodeURIComponent(record.userId)}`}>
          {sessionUserLabel(record)}
        </Link>
      ),
    },
    {
      title: t('users.sessions.colRole'),
      dataIndex: 'role',
      key: 'role',
      render: (value: string | null | undefined) => value || '—',
    },
    {
      title: t('users.sessions.colDevice'),
      key: 'device',
      render: (_, record) => record.deviceName?.trim() || t('common.auth.sessions.unknownDevice'),
    },
    {
      title: t('users.sessions.colClient'),
      dataIndex: 'clientApp',
      key: 'clientApp',
    },
    {
      title: t('users.sessions.colIp'),
      dataIndex: 'ipAddress',
      key: 'ipAddress',
      render: (value: string | null | undefined) => value || '—',
    },
    {
      title: t('users.sessions.colLastActivity'),
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
            <StatusBadge status="info" label={t('users.sessions.thisDevice')} />
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
      title: t('users.sessions.colAction'),
      key: 'action',
      render: (_, record) =>
        record.isCurrent ? null : (
          <Button
            danger
            size="small"
            loading={isMutating}
            onClick={() => setPendingConfirm({ kind: 'revoke', session: record })}
          >
            {t('users.sessions.terminate')}
          </Button>
        ),
    },
  ];

  if (!isSuperAdmin) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('users.sessions.pageTitle')} breadcrumbs={breadcrumbs} />
        <Typography.Paragraph>{t('users.sessions.accessDeniedDescription')}</Typography.Paragraph>
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <AdminPageHeader title={t('users.sessions.pageTitle')} breadcrumbs={breadcrumbs} />
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {t('users.sessions.pageDescription')}
      </Typography.Paragraph>

      <Card
        extra={
          <Space wrap>
            <Input.Search
              allowClear
              placeholder={t('users.sessions.searchPlaceholder')}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              style={{ width: 260 }}
            />
            <Button onClick={() => void refetch()} loading={isFetching}>
              {t('common.buttons.refresh')}
            </Button>
            <Button
              danger
              disabled={sessions.filter((s) => !s.isCurrent).length === 0}
              loading={isMutating}
              onClick={() => setPendingConfirm({ kind: 'revokeAll' })}
            >
              {t('users.sessions.terminateAll')}
            </Button>
          </Space>
        }
      >
        <Table<AdminActiveSession>
          dataSource={filtered}
          columns={columns}
          rowKey="id"
          loading={isLoading}
          pagination={{ pageSize: 25, hideOnSinglePage: true }}
          locale={{
            emptyText: (
              <EmptyState
                title={t('users.sessions.empty')}
                description={t('users.sessions.pageDescription')}
              />
            ),
          }}
        />
      </Card>

      <ConfirmDialog
        open={pendingConfirm?.kind === 'revoke'}
        title={t('users.sessions.terminateConfirmTitle')}
        message={t('users.sessions.terminateConfirmContent')}
        type="danger"
        confirmText={t('users.sessions.terminate')}
        loading={isMutating}
        onConfirm={() => void handleConfirm()}
        onCancel={() => setPendingConfirm(null)}
      />
      <ConfirmDialog
        open={pendingConfirm?.kind === 'revokeAll'}
        title={t('users.sessions.terminateAllConfirmTitle')}
        message={t('users.sessions.terminateAllConfirmContent')}
        type="danger"
        confirmText={t('users.sessions.terminateAll')}
        loading={isMutating}
        onConfirm={() => void handleConfirm()}
        onCancel={() => setPendingConfirm(null)}
      />
    </AdminPageShell>
  );
}
