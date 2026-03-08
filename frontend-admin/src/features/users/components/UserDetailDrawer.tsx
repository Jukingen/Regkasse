'use client';

/**
 * User detail drawer – Details + Activity; bilgi hiyerarşisi: Status/Rolle → Identität → Sonstiges.
 */
import React, { useState } from 'react';
import { Drawer, Tabs, Descriptions, Tag, Typography } from 'antd';
import type { UserInfo } from '@/api/generated/model';
import { UserActivityTimeline } from './UserActivityTimeline';
import { usersCopy } from '../constants/copy';

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
};

export function UserDetailDrawer({ open, onClose, user }: Props) {
  const [activeTab, setActiveTab] = useState('activity');

  if (!user) return null;

  return (
    <Drawer
      title={fullName(user)}
      placement="right"
      width={640}
      onClose={onClose}
      open={open}
      destroyOnClose
    >
      <Tabs
        activeKey={activeTab}
        onChange={setActiveTab}
        items={[
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
                  <Descriptions.Item label={usersCopy.userName}>{user.userName ?? NA}</Descriptions.Item>
                  <Descriptions.Item label={usersCopy.email}>{user.email ?? NA}</Descriptions.Item>
                </Descriptions>
                <Descriptions column={1} size="small" bordered>
                  <Descriptions.Item label={usersCopy.employeeNumber}>{user.employeeNumber ?? NA}</Descriptions.Item>
                  <Descriptions.Item label={usersCopy.lastLogin}>
                    {user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleString('de-DE') : NA}
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
    </Drawer>
  );
}
