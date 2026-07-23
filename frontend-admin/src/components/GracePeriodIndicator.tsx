'use client';

import { Alert, Progress, Typography } from 'antd';
import { useEffect, useMemo, useState } from 'react';

import { useI18n } from '@/i18n';

export type GracePeriodIndicatorProps = {
  expiresAt: Date | string;
  /** Total window length in seconds (for progress %). Defaults from expiresAt - now at mount. */
  totalSeconds?: number;
  onExpire?: () => void;
  className?: string;
  style?: React.CSSProperties;
};

function toDate(value: Date | string): Date {
  return value instanceof Date ? value : new Date(value);
}

function calculateRemainingSeconds(expiresAt: Date): number {
  return Math.max(0, Math.floor((expiresAt.getTime() - Date.now()) / 1000));
}

function formatTime(totalSeconds: number): string {
  const s = Math.max(0, Math.floor(totalSeconds));
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  const sec = s % 60;
  if (h > 0) {
    return `${h}:${String(m).padStart(2, '0')}:${String(sec).padStart(2, '0')}`;
  }
  return `${m}:${String(sec).padStart(2, '0')}`;
}

export function GracePeriodIndicator({
  expiresAt,
  totalSeconds: totalSecondsProp,
  onExpire,
  className,
  style,
}: GracePeriodIndicatorProps) {
  const { t } = useI18n();
  const expires = useMemo(() => toDate(expiresAt), [expiresAt]);
  const [remaining, setRemaining] = useState(() => calculateRemainingSeconds(expires));
  const [totalSeconds] = useState(() => {
    if (totalSecondsProp && totalSecondsProp > 0) return totalSecondsProp;
    const initial = calculateRemainingSeconds(expires);
    return Math.max(initial, 1);
  });

  useEffect(() => {
    setRemaining(calculateRemainingSeconds(expires));
    const interval = window.setInterval(() => {
      const next = calculateRemainingSeconds(expires);
      setRemaining(next);
      if (next <= 0) {
        window.clearInterval(interval);
        onExpire?.();
      }
    }, 1000);
    return () => window.clearInterval(interval);
  }, [expires, onExpire]);

  const percent = Math.min(100, Math.max(0, (remaining / totalSeconds) * 100));

  return (
    <Alert
      className={className}
      style={style}
      type="warning"
      showIcon
      title={t('common.gracePeriod.activeTitle')}
      description={
        <div>
          <Typography.Text>
            {t('common.gracePeriod.remainingPrefix')}{' '}
            <Typography.Text strong>{formatTime(remaining)}</Typography.Text>{' '}
            {t('common.gracePeriod.remainingSuffix')}
          </Typography.Text>
          <Progress
            percent={percent}
            status={percent < 20 ? 'exception' : 'active'}
            showInfo={false}
            style={{ marginTop: 8, marginBottom: 0 }}
          />
        </div>
      }
    />
  );
}
