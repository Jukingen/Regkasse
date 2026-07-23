'use client';

import { Alert, Card, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import React, { useMemo } from 'react';

import type { RoleWithPermissionsDto } from '@/features/users/api/usersGateway';
import { resolvePermissionGroupLabel } from '@/features/users/utils/permissionDisplayLabel';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n/I18nProvider';
import { PERMISSIONS } from '@/shared/auth/permissions';

type RoleMatrixOverviewProps = {
  roles: RoleWithPermissionsDto[] | undefined;
  loading: boolean;
  error: boolean;
  onRetry: () => void;
};

const CANONICAL = new Set([
  'SuperAdmin',
  'Manager',
  'Cashier',
  'Waiter',
  'Kitchen',
  'ReportViewer',
  'Accountant',
]);

export function RoleMatrixOverview({ roles, loading, error, onRetry }: RoleMatrixOverviewProps) {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const canManageRoles = hasPermission(PERMISSIONS.ROLE_MANAGE);

  const rows = useMemo(
    () =>
      [...(roles ?? [])].sort((a, b) => {
        const systemA = a.isSystemRole ? 0 : 1;
        const systemB = b.isSystemRole ? 0 : 1;
        if (systemA !== systemB) return systemA - systemB;
        return a.roleName.localeCompare(b.roleName, 'de');
      }),
    [roles]
  );

  const columns: ColumnsType<RoleWithPermissionsDto> = useMemo(
    () => [
      {
        title: t('access.matrix.columnRole'),
        dataIndex: 'roleName',
        key: 'roleName',
        render: (name: string, record) => (
          <span>
            {CANONICAL.has(name) ? t(`users.roles.displayNames.${name}`) : name}
            {record.isSystemRole ? (
              <Tag style={{ marginLeft: 8 }} color="blue">
                {t('access.matrix.systemRoleTag')}
              </Tag>
            ) : null}
          </span>
        ),
      },
      {
        title: t('access.matrix.columnUsers'),
        dataIndex: 'userCount',
        key: 'userCount',
        width: 100,
        align: 'right',
      },
      {
        title: t('access.matrix.columnPosAdmin'),
        key: 'ui',
        width: 180,
        render: (_, record) => {
          const cap = record.uiCapabilities;
          if (!cap) return '—';
          if (cap.posLogin && cap.adminLogin) return t('users.roleDrawer.badgePosAndAdmin');
          if (cap.posLogin) return t('users.roleDrawer.badgePosUi');
          if (cap.adminLogin) return t('users.roleDrawer.badgeAdminUi');
          return '—';
        },
      },
      {
        title: t('access.matrix.columnPermissionCount'),
        key: 'permCount',
        width: 140,
        align: 'right',
        render: (_, record) => record.permissions?.length ?? 0,
      },
      {
        title: t('access.matrix.columnGroups'),
        key: 'groups',
        render: (_, record) =>
          (record.permissionGroups ?? []).map((g) => (
            <Tag key={g.groupKey}>{resolvePermissionGroupLabel(g.groupKey, t)}</Tag>
          )),
      },
    ],
    [t]
  );

  if (error) {
    return (
      <Alert
        type="error"
        showIcon
        title={t('access.matrix.loadErrorTitle')}
        action={<Typography.Link onClick={onRetry}>{t('common.buttons.retry')}</Typography.Link>}
      />
    );
  }

  return (
    <Card>
      <Typography.Paragraph type="secondary">{t('access.matrix.intro')}</Typography.Paragraph>
      <Typography.Paragraph type="secondary" style={{ fontSize: 12 }}>
        {canManageRoles ? (
          <>
            {t('access.matrix.editHint')}{' '}
            <Link href="/admin/access/roles">{t('nav.accessRoles')}</Link>
            {' · '}
          </>
        ) : null}
        <Link href="/audit-logs">{t('nav.auditLogs')}</Link>
      </Typography.Paragraph>
      <Table
        rowKey="roleName"
        loading={loading}
        dataSource={rows}
        columns={columns}
        pagination={false}
        scroll={{ x: 960 }}
        size="middle"
      />
    </Card>
  );
}
