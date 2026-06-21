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
import { useI18n } from '@/i18n/I18nProvider';
import { formatDateTime } from '@/i18n/formatting';
import type { UpdateAdminUsernameResponse } from '@/features/users/api/users';

const { Text } = Typography;

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
  const na = t('users.list.branchNotAvailable');
  const userId = user?.id ?? null;
  const { data: memberships = [], isLoading: tenantsLoading } = useAdminUserTenants(userId, open && !!userId);

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
      <Tabs
        activeKey={activeTab}
        onChange={setActiveTab}
        items={[
          {
            key: 'report',
            label: t('users.activity.reportTabLabel'),
            children: (
              <>
                {user.id && (
                  <Link
                    href={`/admin/reports/user-activity?userId=${encodeURIComponent(user.id)}`}
                    style={{ marginBottom: 12, display: 'inline-block' }}
                  >
                    <Button type="link" size="small">
                      {t('users.activity.openFullReport')}
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
            label: t('users.activity.tabLabel'),
            children: (
              <UserActivityTimeline
                userId={user.id ?? ''}
                userName={fullName(user)}
              />
            ),
          },
          {
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
                  <Descriptions.Item label={t('users.tabs.tenant.columnTenant')}>
                    <UserTenantSummary
                      userRole={user.role}
                      memberships={memberships}
                      loading={tenantsLoading}
                    />
                  </Descriptions.Item>
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
                  <Descriptions.Item label={t('users.list.columnEmail')}>{user.email ?? na}</Descriptions.Item>
                </Descriptions>
                <Descriptions column={1} size="small" bordered>
                  <Descriptions.Item label={t('users.form.employeeNumber')}>{user.employeeNumber ?? na}</Descriptions.Item>
                  <Descriptions.Item label={t('users.list.columnLastLogin')}>
                    {user.lastLoginAt ? formatDateTime(user.lastLoginAt, formatLocale) : na}
                  </Descriptions.Item>
                </Descriptions>
                {user.notes ? (
                  <>
                    <Text type="secondary" style={{ fontSize: 12 }}>{t('users.form.notes')}</Text>
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
