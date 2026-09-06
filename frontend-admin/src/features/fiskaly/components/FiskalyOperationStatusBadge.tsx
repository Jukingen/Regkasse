'use client';

import {
  CheckCircleOutlined,
  ClockCircleOutlined,
  CloseCircleOutlined,
  LoadingOutlined,
} from '@ant-design/icons';
import { Progress, Space, Tag, Typography } from 'antd';
import type { ReactNode } from 'react';

import {
  fiskalyHistoryProgressPercent,
  normalizeFiskalyLiveStatus,
  type FiskalyLiveStatus,
} from '@/features/fiskaly/fiskalyOperationStatus';
import { useI18n } from '@/i18n';

import styles from './fiskalyOperationStatus.module.css';

const STATUS_LABEL_KEY: Record<FiskalyLiveStatus, 'tseFiskaly.history.statusPending' | 'tseFiskaly.history.statusProcessing' | 'tseFiskaly.history.statusCompleted' | 'tseFiskaly.history.statusFailed'> = {
  Pending: 'tseFiskaly.history.statusPending',
  Processing: 'tseFiskaly.history.statusProcessing',
  Success: 'tseFiskaly.history.statusCompleted',
  Failed: 'tseFiskaly.history.statusFailed',
};

function statusIcon(status: FiskalyLiveStatus): ReactNode {
  switch (status) {
    case 'Pending':
      return <ClockCircleOutlined className={styles.pulse} aria-hidden />;
    case 'Processing':
      return <LoadingOutlined className={styles.spinIcon} aria-hidden />;
    case 'Success':
      return <CheckCircleOutlined aria-hidden />;
    case 'Failed':
      return <CloseCircleOutlined aria-hidden />;
  }
}

function tagColor(status: FiskalyLiveStatus): string {
  switch (status) {
    case 'Pending':
      return 'warning';
    case 'Processing':
      return 'processing';
    case 'Success':
      return 'success';
    case 'Failed':
      return 'error';
  }
}

export function FiskalyOperationStatusBadge({
  status,
  showIcon = true,
}: {
  status: string | null | undefined;
  showIcon?: boolean;
}) {
  const { t } = useI18n();
  const live = normalizeFiskalyLiveStatus(status);
  return (
    <Tag color={tagColor(live)} className={styles.badge}>
      {showIcon ? statusIcon(live) : null}
      {t(STATUS_LABEL_KEY[live])}
    </Tag>
  );
}

export function FiskalyOperationLivePanel({
  status,
  progressPercent,
  hint,
}: {
  status: string | null | undefined;
  progressPercent?: number | null;
  hint?: string;
}) {
  const live = normalizeFiskalyLiveStatus(status);
  const percent = fiskalyHistoryProgressPercent(status, progressPercent);
  const progressStatus = live === 'Failed' ? 'exception' : live === 'Success' ? 'success' : 'active';

  return (
    <div className={styles.panel}>
      <Space>
        <FiskalyOperationStatusBadge status={live} />
      </Space>
      <Progress percent={percent} status={progressStatus} showInfo />
      {hint ? (
        <Typography.Text type="secondary">{hint}</Typography.Text>
      ) : null}
    </div>
  );
}
