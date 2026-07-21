'use client';

/**
 * User detail drawer – Details + Activity; bilgi hiyerarşisi: Status/Rolle → Identität → Mandant → Sonstiges.
 */
import { EditOutlined } from '@ant-design/icons';
import type { TabsProps } from 'antd';
import { Button, Descriptions, Drawer, Space, Tabs, Tag, Typography } from 'antd';
import Link from 'next/link';
import React, { useEffect, useMemo, useState } from 'react';

import type { UserInfo } from '@/api/generated/model';
import type { UpdateAdminUsernameResponse } from '@/features/users/api/users';
import { useAdminUserTenants } from '@/features/users/hooks/useAdminUserTenants';
import { useI18n } from '@/i18n/I18nProvider';
import { formatDateTime } from '@/i18n/formatting';
import { useStaffPolicy } from '@/shared/auth/staffPolicy';

import { EditUsernameModal } from './EditUsernameModal';
import { UserActivityReportPanel } from './UserActivityReportPanel';
import { UserActivityTimeline } from './UserActivityTimeline';
import { UserTenantSummary } from './UserTenantSummary';

const { Text } = Typography;

function fullName(record: UserInfo): string {
  const first = record.firstName ?? '';
  const last = record.lastName ?? '';
  const name = `${first} ${last}`.trim();
  return name || record.userName || record.id || '—';
}

type DrawerContext = 'admin' | 'staff';

type Props = {
  open: boolean;
  onClose: () => void;
  user: UserInfo | null;
  canEditUsername?: boolean;
  onUsernameUpdated?: (userId: string, result: UpdateAdminUsernameResponse) => void;
  /** Staff hub: default activity tab, hide report tab when report.view is missing. */
  context?: DrawerContext;
};

function resolveDefaultTab(
  context: DrawerContext,
  canViewActivityReport: boolean,
  canViewActivity: boolean
): string {
  if (context === 'staff') {
    if (canViewActivity) return 'activity';
    if (canViewActivityReport) return 'report';
    return 'details';
  }
  if (canViewActivityReport) return 'report';
  if (canViewActivity) return 'activity';
  return 'details';
}

export function UserDetailDrawer({
  open,
  onClose,
  user,
  canEditUsername = false,
  onUsernameUpdated,
  context = 'admin',
}: Props) {
  const staffPolicy = useStaffPolicy();
  const [activeTab, setActiveTab] = useState('details');
  const [usernameModalOpen, setUsernameModalOpen] = useState(false);
  const { t, formatLocale } = useI18n();
  const na = t('users.list.branchNotAvailable');
  const userId = user?.id ?? null;

  const canViewTenantMemberships = staffPolicy.canViewTenantMemberships;
  const { data: memberships = [], isLoading: tenantsLoading } = useAdminUserTenants(
    userId,
    open && !!userId && canViewTenantMemberships
  );

  const defaultTab = useMemo(
    () =>
      resolveDefaultTab(context, staffPolicy.canViewActivityReport, staffPolicy.canViewActivity),
    [context, staffPolicy.canViewActivity, staffPolicy.canViewActivityReport]
  );

  useEffect(() => {
    if (open) {
      setActiveTab(defaultTab);
    }
  }, [open, defaultTab, userId]);

  const tabItems = useMemo((): TabsProps['items'] => {
    if (!user) return [];

    const items: NonNullable<TabsProps['items']> = [];

    if (staffPolicy.canViewActivityReport) {
      items.push({
        key: 'report',
        label: t('users.activity.reportTabLabel'),
        children: (
          <>
            {user.id ? (
              <Link
                href={`/admin/reports/user-activity?userId=${encodeURIComponent(user.id)}`}
                style={{ marginBottom: 12, display: 'inline-block' }}
              >
                <Button type="link" size="small">
                  {t('users.activity.openFullReport')}
                </Button>
              </Link>
            ) : null}
            <UserActivityReportPanel userId={user.id ?? ''} userName={fullName(user)} />
          </>
        ),
      });
    }

    if (staffPolicy.canViewActivity) {
      items.push({
        key: 'activity',
        label: t('users.activity.tabLabel'),
        children: <UserActivityTimeline userId={user.id ?? ''} userName={fullName(user)} />,
      });
    }

    items.push({
      key: 'details',
      label: t('users.list.details'),
      children: (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
          <Descriptions column={1} size="small" bordered title={null}>
            <Descriptions.Item label={t('users.list.columnStatus')}>
              <Tag color={user.isActive ? 'green' : 'red'}>
                {user.isActive ? t('users.list.statusActive') : t('users.list.statusInactive')}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label={t('users.list.columnRole')}>
              <Tag color="gold">{user.role ?? na}</Tag>
            </Descriptions.Item>
            {canViewTenantMemberships ? (
              <Descriptions.Item label={t('users.tabs.tenant.columnTenant')}>
                <UserTenantSummary
                  userRole={user.role}
                  memberships={memberships}
                  loading={tenantsLoading}
                />
              </Descriptions.Item>
            ) : null}
            <Descriptions.Item label={t('users.form.userName')}>
              <Space wrap>
                <span>{user.userName?.trim() || na}</span>
                {canEditUsername && user.id ? (
                  <Button
                    type="link"
                    size="small"
                    icon={<EditOutlined />}
                    onClick={() => setUsernameModalOpen(true)}
                  >
                    {t('users.username.changeAction')}
                  </Button>
                ) : null}
              </Space>
            </Descriptions.Item>
            <Descriptions.Item label={t('users.list.columnEmail')}>
              {user.email ?? na}
            </Descriptions.Item>
          </Descriptions>
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label={t('users.form.employeeNumber')}>
              {user.employeeNumber ?? na}
            </Descriptions.Item>
            <Descriptions.Item label={t('users.list.columnLastLogin')}>
              {user.lastLoginAt ? formatDateTime(user.lastLoginAt, formatLocale) : na}
            </Descriptions.Item>
          </Descriptions>
          {user.notes ? (
            <>
              <Text type="secondary" style={{ fontSize: 12 }}>
                {t('users.form.notes')}
              </Text>
              <Text>{user.notes}</Text>
            </>
          ) : null}
        </div>
      ),
    });

    return items;
  }, [
    canEditUsername,
    canViewTenantMemberships,
    formatLocale,
    memberships,
    staffPolicy.canViewActivity,
    staffPolicy.canViewActivityReport,
    t,
    tenantsLoading,
    user,
    na,
  ]);

  if (!user) return null;

  return (
    <Drawer
      title={fullName(user)}
      placement="right"
      size={840}
      onClose={onClose}
      open={open}
      destroyOnHidden
    >
      <Tabs activeKey={activeTab} onChange={setActiveTab} items={tabItems} />
      {user.id ? (
        <EditUsernameModal
          open={usernameModalOpen}
          userId={user.id}
          currentUsername={user.userName ?? ''}
          userEmail={user.email}
          onClose={() => setUsernameModalOpen(false)}
          onSuccess={(result) => onUsernameUpdated?.(user.id!, result)}
        />
      ) : null}
    </Drawer>
  );
}
