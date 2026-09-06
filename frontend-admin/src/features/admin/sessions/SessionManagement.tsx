'use client';

import { Alert, Button, Card, Input, Select, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useMemo, useState } from 'react';

import type { AdminActiveSession } from '@/api/manual/adminSessions';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { EmptyState } from '@/components/EmptyState';
import { StatusBadge } from '@/components/StatusBadge';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { AccessSecondaryNav } from '@/features/access/components/AccessSecondaryNav';
import { formatSessionDevice } from '@/features/admin/sessions/sessionDeviceLabel';
import { formatSessionDuration } from '@/features/admin/sessions/sessionDuration';
import { ROLES_CANONICAL } from '@/features/auth/constants/roles';
import { useAdminSessions } from '@/features/sessions/hooks/useAdminSessions';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import dayjs from '@/lib/dayjs';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

type PendingConfirm =
  | { kind: 'revoke'; session: AdminActiveSession }
  | { kind: 'revokeAll' }
  | { kind: 'revokeBulk'; sessionIds: string[] }
  | null;

function sessionUserLabel(session: AdminActiveSession): string {
  return session.userName?.trim() || session.displayName?.trim() || session.email?.trim() || session.userId;
}

export function SessionManagement() {
  const { t } = useI18n();
  const notify = useNotify();
  const { isSuperAdmin, isManager, hasPermission } = usePermissions();
  const canView = hasPermission(PERMISSIONS.USER_VIEW);
  const canManage = hasPermission(PERMISSIONS.USER_MANAGE);

  const [userDraft, setUserDraft] = useState('');
  const [appliedUser, setAppliedUser] = useState('');
  const [role, setRole] = useState<string | undefined>();
  const [tenantId, setTenantId] = useState<string | undefined>();
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [pendingConfirm, setPendingConfirm] = useState<PendingConfirm>(null);

  const listParams = useMemo(
    () => ({
      search: appliedUser,
      role,
      status: 'active' as const,
      tenantId: isSuperAdmin ? tenantId : undefined,
    }),
    [appliedUser, isSuperAdmin, role, tenantId]
  );

  const { sessions, isLoading, isFetching, isError, error, refetch, terminateOne, terminateAll, terminateBulk } =
    useAdminSessions(canView, listParams);
  const { tenants, isLoading: tenantsLoading } = useTenantList({ enabled: isSuperAdmin });

  const breadcrumbs = buildPlatformAdminBreadcrumbs(t, 'administration', {
    title: t('nav.sessions'),
  });

  const isMutating =
    terminateOne.isPending || terminateAll.isPending || terminateBulk.isPending;

  const applySearch = () => {
    setAppliedUser(userDraft.trim());
  };

  const handleConfirm = async () => {
    if (!pendingConfirm) return;
    try {
      if (pendingConfirm.kind === 'revoke') {
        await terminateOne.mutateAsync(pendingConfirm.session.id);
        notify.successKey('users.sessions.terminated');
      } else if (pendingConfirm.kind === 'revokeBulk') {
        const result = await terminateBulk.mutateAsync(pendingConfirm.sessionIds);
        notify.success(
          t('users.sessions.terminatedCount', { count: String(result.terminatedCount) })
        );
        setSelectedIds([]);
      } else {
        const result = await terminateAll.mutateAsync();
        notify.success(
          t('users.sessions.terminatedCount', { count: String(result.terminatedCount) })
        );
        setSelectedIds([]);
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
      render: (_, record) =>
        formatSessionDevice(record, t('common.auth.sessions.unknownDevice')),
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
      render: (value: string) => (value ? dayjs(value).format('HH:mm') : '—'),
    },
    {
      title: t('users.sessions.colDuration'),
      key: 'duration',
      render: (_, record) => formatSessionDuration(record.durationSeconds),
    },
    {
      title: t('users.sessions.colStatus'),
      key: 'status',
      render: (_, record) => (
        <StatusBadge
          status={record.isActive ? 'active' : 'expired'}
          label={
            record.isActive
              ? t('common.auth.sessions.statusActive')
              : t('users.sessions.statusExpired')
          }
        />
      ),
    },
    {
      title: t('users.sessions.colAction'),
      key: 'action',
      render: (_, record) => {
        if (record.isCurrent) {
          return <StatusBadge status="info" label={t('users.sessions.thisDevice')} />;
        }
        if (!canManage || !record.isActive) return null;
        return (
          <Button
            danger
            size="small"
            loading={isMutating}
            onClick={() => setPendingConfirm({ kind: 'revoke', session: record })}
          >
            {t('users.sessions.logout')}
          </Button>
        );
      },
    },
  ];

  if (!canView) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('users.sessions.pageTitle')} breadcrumbs={breadcrumbs} />
        <Typography.Paragraph>{t('users.sessions.accessDeniedDescription')}</Typography.Paragraph>
      </AdminPageShell>
    );
  }

  const selectedLogoutIds = selectedIds.filter((id) => {
    const row = sessions.find((s) => s.id === id);
    return row != null && !row.isCurrent && row.isActive;
  });

  return (
    <AdminPageShell>
      {!isSuperAdmin && isManager ? <AccessSecondaryNav /> : null}
      <AdminPageHeader title={t('users.sessions.pageTitle')} breadcrumbs={breadcrumbs} />

      {isError ? (
        <Alert
          type="error"
          showIcon
          title={t('users.sessions.loadFailed')}
          description={
            <ApiErrorAlertDescription
              t={t}
              error={error}
              logContext="AdminSessions.load"
              fallbackKey="users.sessions.loadFailedHint"
            />
          }
          action={
            <Button size="small" loading={isFetching} onClick={() => void refetch()}>
              {t('common.buttons.retry')}
            </Button>
          }
        />
      ) : (
        <Card>
          <Space wrap style={{ marginBottom: 16 }}>
            <Space.Compact>
              <Button disabled>{t('users.sessions.filterUser')}</Button>
              <Input
                allowClear
                placeholder={t('users.sessions.searchPlaceholder')}
                value={userDraft}
                onChange={(e) => setUserDraft(e.target.value)}
                onPressEnter={applySearch}
                style={{ width: 184 }}
              />
            </Space.Compact>
            <Select
              allowClear
              placeholder={t('users.sessions.filterRole')}
              value={role}
              onChange={(value) => setRole(value)}
              style={{ minWidth: 160 }}
              options={ROLES_CANONICAL.map((r) => ({ value: r, label: r }))}
            />
            {isSuperAdmin ? (
              <Select
                allowClear
                placeholder={t('users.sessions.filterTenant')}
                value={tenantId}
                onChange={(value) => setTenantId(value)}
                loading={tenantsLoading}
                style={{ minWidth: 180 }}
                options={tenants.map((row) => ({ value: row.id, label: row.name }))}
              />
            ) : null}
            <Button type="primary" onClick={applySearch} loading={isFetching}>
              {t('users.sessions.searchButton')}
            </Button>
          </Space>

          <Table<AdminActiveSession>
            dataSource={sessions}
            columns={columns}
            rowKey="id"
            loading={isLoading}
            scroll={{ x: 960 }}
            rowSelection={
              canManage
                ? {
                    selectedRowKeys: selectedIds,
                    onChange: (keys) => setSelectedIds(keys.map(String)),
                    getCheckboxProps: (record) => ({
                      disabled: record.isCurrent || !record.isActive,
                    }),
                  }
                : undefined
            }
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

          {canManage ? (
            <Space wrap style={{ marginTop: 16 }}>
              <Button
                danger
                disabled={selectedLogoutIds.length === 0}
                loading={isMutating}
                onClick={() =>
                  setPendingConfirm({ kind: 'revokeBulk', sessionIds: selectedLogoutIds })
                }
              >
                {t('users.sessions.logoutBulk')}
              </Button>
              <Button
                danger
                disabled={sessions.filter((s) => !s.isCurrent && s.isActive).length === 0}
                loading={isMutating}
                onClick={() => setPendingConfirm({ kind: 'revokeAll' })}
              >
                {t('users.sessions.logoutAllOthers')}
              </Button>
            </Space>
          ) : null}
        </Card>
      )}

      <ConfirmDialog
        open={pendingConfirm?.kind === 'revoke'}
        title={t('users.sessions.terminateConfirmTitle')}
        message={t('users.sessions.terminateConfirmContent')}
        type="danger"
        confirmText={t('users.sessions.logout')}
        loading={isMutating}
        onConfirm={() => void handleConfirm()}
        onCancel={() => setPendingConfirm(null)}
      />
      <ConfirmDialog
        open={pendingConfirm?.kind === 'revokeAll'}
        title={t('users.sessions.logoutAllOthersConfirmTitle')}
        message={t('users.sessions.logoutAllOthersConfirmContent')}
        type="danger"
        confirmText={t('users.sessions.logoutAllOthers')}
        loading={isMutating}
        onConfirm={() => void handleConfirm()}
        onCancel={() => setPendingConfirm(null)}
      />
      <ConfirmDialog
        open={pendingConfirm?.kind === 'revokeBulk'}
        title={t('users.sessions.logoutBulkConfirmTitle')}
        message={t('users.sessions.logoutBulkConfirmContent', {
          count: String(pendingConfirm?.kind === 'revokeBulk' ? pendingConfirm.sessionIds.length : 0),
        })}
        type="danger"
        confirmText={t('users.sessions.logoutBulk')}
        loading={isMutating}
        onConfirm={() => void handleConfirm()}
        onCancel={() => setPendingConfirm(null)}
      />
    </AdminPageShell>
  );
}

export { SessionManagement as AdminSessionsPage };
