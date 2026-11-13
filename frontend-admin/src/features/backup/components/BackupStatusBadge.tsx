'use client';

import {
  CheckCircleOutlined,
  ClockCircleOutlined,
  CloseCircleOutlined,
  ExclamationCircleOutlined,
  SyncOutlined,
} from '@ant-design/icons';
import { Tag } from 'antd';
import React from 'react';

import {
  backupRunTrafficLightTagColor,
  resolveBackupRunTrafficLight,
} from '@/features/backup/logic/backupRunTrafficLight';
import { resolveBackupRunStatusUiKey } from '@/features/backup/logic/backupRunTablePresentation';
import { useI18n } from '@/i18n';

export interface BackupStatusBadgeProps {
  status: number | undefined;
}

export function BackupStatusBadge({ status }: BackupStatusBadgeProps) {
  const { t } = useI18n();
  const uiKey = resolveBackupRunStatusUiKey(status);
  const label =
    uiKey === 'unknown'
      ? t('backupDr.summary.unknown')
      : t(`backupDr.runsTable.statusLabels.${uiKey}`);
  const color = backupRunTrafficLightTagColor(resolveBackupRunTrafficLight(status));

  switch (uiKey) {
    case 'succeeded':
      return (
        <Tag color={color} icon={<CheckCircleOutlined />}>
          {label}
        </Tag>
      );
    case 'failed':
    case 'verificationFailed':
      return (
        <Tag color={color} icon={uiKey === 'failed' ? <CloseCircleOutlined /> : <ExclamationCircleOutlined />}>
          {label}
        </Tag>
      );
    case 'running':
    case 'awaitingVerification':
      return (
        <Tag color={color} icon={<SyncOutlined spin />}>
          {label}
        </Tag>
      );
    case 'queued':
      return (
        <Tag color={color} icon={<ClockCircleOutlined />}>
          {label}
        </Tag>
      );
    case 'cancelled':
      return (
        <Tag color={color} icon={<CloseCircleOutlined />}>
          {label}
        </Tag>
      );
    default:
      return <Tag>{label}</Tag>;
  }
}
