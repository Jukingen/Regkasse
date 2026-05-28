'use client';

/**
 * User detail drawer – Details + Activity; bilgi hiyerarşisi: Status/Rolle → Identität → Mandant → Sonstiges.
 */
import React, { useState } from 'react';
import { Drawer, Tabs, Descriptions, Tag, Typography, Button, Space } from 'antd';
import { EditOutlined } from '@ant-design/icons';
import type { UserInfo } from '@/api/generated/model';
import { EditUsernameModal } from './EditUsernameModal';
import { UserActivityTimeline } from './UserActivityTimeline';
import Link from 'next/link';
import { UserActivityReportPanel } from './UserActivityReportPanel';
import { UserTenantSummary } from './UserTenantSummary';
import { useAdminUserTenants } from '@/features/users/hooks/useAdminUserTenants';
import { usersCopy } from '../constants/copy';
import { useI18n } from '@/i18n/I18nProvider';
import { formatDateTime } from '@/i18n/formatting';
import type { UpdateAdminUsernameResponse } from '@/features/users/api/users';

const { Text } = Typography;
const NA = usersCopy.branchNotAvailable;

function fullName(record: UserInfo): string {
  const first = record.firstName ?? '';
  const last = record.lastName ?? '';
  const name = `${first} ${last}`.trim();
  return name || record.userName || record.id || '—';
}

type Props = {
  open: boolean;
  onClose: () => void;
  user: UserInfo | null;
  canEditUsername?: boolean;
  onUsernameUpdated?: (userId: string, result: UpdateAdminUsernameResponse) => void;
};

export function UserDetailDrawer({
  open,
  onClose,
  user,
  canEditUsername = false,
  onUsernameUpdated,
}: Props) {
  const [activeTab, setActiveTab] = useState('report');
  const [usernameModalOpen, setUsernameModalOpen] = useState(false);
  const { t, formatLocale } = useI18n();
  const userId = user?.id ?? null;
  const { data: memberships = [], isLoading: tenantsLoading } = useAdminUserTenants(userId, open && !!userId);

  if (!user) return null;

  return (
    <Drawer
      title={fullName(user)}
      placement="right"
      width={840}
      onClose={onClose}
      open={open}
      destroyOnHidden
    >
      <Tabs
        activeKey={activeTab}
        onChange={setActiveTab}
        items={[
          {
            key: 'report',
            label: usersCopy.activityReport,
            children: (
              <>
                {user.id && (
                  <Link
                    href={`/admin/reports/user-activity?userId=${encodeURIComponent(user.id)}`}
                    style={{ marginBottom: 12, display: 'inline-block' }}
                  >
                    <Button type="link" size="small">
                      {usersCopy.openFullActivityReport}
                    </Button>
                  </Link>
                )}
                <UserActivityReportPanel
                  userId={user.id ?? ''}
                  userName={fullName(user)}
                />
              </>
            ),
          },
          {
            key: 'activity',
            label: usersCopy.activity,
            children: (
              <UserActivityTimeline
                userId={user.id ?? ''}
                userName={fullName(user)}
              />
            ),
          },
          {
            key: 'details',
            label: usersCopy.details,
            children: (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
                <Descriptions column={1} size="small" bordered title={null}>
                  <Descriptions.Item label={usersCopy.status}>
                    <Tag color={user.isActive ? 'green' : 'red'}>
                      {user.isActive ? usersCopy.statusActive : usersCopy.statusInactive}
                    </Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label={usersCopy.role}>
                    <Tag color="gold">{user.role ?? NA}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label={t('users.tabs.tenant.columnTenant')}>
                    <UserTenantSummary
                      userRole={user.role}
                      memberships={memberships}
                      loading={tenantsLoading}
                    />
                  </Descriptions.Item>
                  <Descriptions.Item label={usersCopy.userName}>
                    <Space wrap>
                      <span>{user.userName?.trim() || NA}</span>
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
                  <Descriptions.Item label={usersCopy.email}>{user.email ?? NA}</Descriptions.Item>
                </Descriptions>
                <Descriptions column={1} size="small" bordered>
                  <Descriptions.Item label={usersCopy.employeeNumber}>{user.employeeNumber ?? NA}</Descriptions.Item>
                  <Descriptions.Item label={usersCopy.lastLogin}>
                    {user.lastLoginAt ? formatDateTime(user.lastLoginAt, formatLocale) : '—'}
                  </Descriptions.Item>
                </Descriptions>
                {user.notes ? (
                  <>
                    <Text type="secondary" style={{ fontSize: 12 }}>{usersCopy.notes}</Text>
                    <Text>{user.notes}</Text>
                  </>
                ) : null}
              </div>
            ),
          },
        ]}
      />
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
