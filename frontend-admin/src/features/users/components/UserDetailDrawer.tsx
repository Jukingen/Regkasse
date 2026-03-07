'use client';

/**
 * User detail drawer with Details + Activity timeline tabs (audit API).
 */
import React, { useState } from 'react';
import { Drawer, Tabs, Descriptions, Tag } from 'antd';
import type { UserInfo } from '@/api/generated/model';
import { UserActivityTimeline } from './UserActivityTimeline';
import { usersCopy } from '../constants/copy';

function fullName(record: UserInfo): string {
  const first = record.firstName ?? '';
  const last = record.lastName ?? '';
  return `${first} ${last}`.trim() || record.userName ?? record.id ?? '—';
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
              <Descriptions column={1} size="small" bordered>
                <Descriptions.Item label={usersCopy.email}>{user.email ?? usersCopy.branchNotAvailable}</Descriptions.Item>
                <Descriptions.Item label={usersCopy.userName}>{user.userName ?? usersCopy.branchNotAvailable}</Descriptions.Item>
                <Descriptions.Item label={usersCopy.role}>
                  <Tag color="gold">{user.role ?? usersCopy.branchNotAvailable}</Tag>
                </Descriptions.Item>
                <Descriptions.Item label={usersCopy.status}>
                  <Tag color={user.isActive ? 'green' : 'red'}>
                    {user.isActive ? usersCopy.statusActive : usersCopy.statusInactive}
                  </Tag>
                </Descriptions.Item>
                <Descriptions.Item label={usersCopy.employeeNumber}>{user.employeeNumber ?? usersCopy.branchNotAvailable}</Descriptions.Item>
                <Descriptions.Item label={usersCopy.lastLogin}>
                  {user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleString('de-DE') : usersCopy.branchNotAvailable}
                </Descriptions.Item>
                {user.notes ? (
                  <Descriptions.Item label={usersCopy.notes}>{user.notes}</Descriptions.Item>
                ) : null}
              </Descriptions>
            ),
          },
        ]}
      />
    </Drawer>
  );
}
