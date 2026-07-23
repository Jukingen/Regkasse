'use client';

import {
  BellOutlined,
  CloudOutlined,
  DatabaseOutlined,
  ExperimentOutlined,
  KeyOutlined,
  SafetyCertificateOutlined,
  SendOutlined,
  ShopOutlined,
  UserOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import type { ReactNode } from 'react';

import type { ActivityDto } from '@/api/manual/activityEvents';
import { isPermissionActivityType } from '@/features/activity-notifications/activityTypes';

function iconForType(type: string): ReactNode {
  if (isPermissionActivityType(type)) {
    return type === 'SystemPermissionChange' || type === 'RoleDeleted' ? (
      <SafetyCertificateOutlined />
    ) : (
      <KeyOutlined />
    );
  }
  if (type.startsWith('User')) {
    return <UserOutlined />;
  }
  if (type.startsWith('CashRegister')) {
    return <ShopOutlined />;
  }
  if (type.startsWith('License')) {
    return <WarningOutlined />;
  }
  if (type === 'OfflineQueueGrowing') {
    return <CloudOutlined />;
  }
  if (type.includes('FinanzOnline')) {
    return <SendOutlined />;
  }
  if (type.startsWith('Backup')) {
    return <DatabaseOutlined />;
  }
  if (type.startsWith('RestoreDrill')) {
    return <ExperimentOutlined />;
  }
  return <BellOutlined />;
}

type Props = {
  activity: ActivityDto;
};

export function NotificationIcon({ activity }: Props) {
  const color =
    activity.severity === 'Critical' || activity.severity === 'Error'
      ? '#cf1322'
      : activity.severity === 'Warning'
        ? '#d48806'
        : '#389e0d';

  return (
    <span style={{ color, fontSize: 18 }} aria-hidden>
      {iconForType(activity.type)}
    </span>
  );
}
