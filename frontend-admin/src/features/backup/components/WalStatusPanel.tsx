'use client';

import { Alert, Card, Col, Row, Spin, Tag, Tooltip, Typography } from 'antd';
import { useEffect, useRef } from 'react';

import { useWalStatus } from '@/features/backup/hooks/useWalStatus';
import type { WalArchiveHealthLevel, WalStatus } from '@/features/backup/logic/walStatusApi';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { translateApiError } from '@/lib/api/errorTranslator';
import dayjs from '@/lib/dayjs';
import { useDateFormatter } from '@/lib/hooks/useDateFormatter';

function healthTone(level: WalArchiveHealthLevel): 'green' | 'amber' | 'red' {
  if (level === 'Healthy') return 'green';
  if (level === 'Degraded') return 'amber';
  return 'red';
}

function tagColor(tone: 'green' | 'amber' | 'red'): string {
  if (tone === 'green') return 'green';
  if (tone === 'amber') return 'gold';
  return 'red';
}

function HealthBadge({ status }: { status: WalStatus }) {
  const { t } = useI18n();
  const tone = healthTone(status.healthLevel);
  const labels: Record<WalArchiveHealthLevel, string> = {
    Healthy: t('backupDr.walStatus.health.healthy'),
    Degraded: t('backupDr.walStatus.health.degraded'),
    Critical: t('backupDr.walStatus.health.critical'),
  };
  return (
    <Tooltip title={status.healthReason}>
      <span
        data-testid="wal-status-health"
        data-health={status.healthLevel}
        data-tone={tone}
        data-reason={status.healthReason}
      >
        <Tag color={tagColor(tone)}>{labels[status.healthLevel]}</Tag>
      </span>
    </Tooltip>
  );
}

/**
 * Live WAL archival health. Sits above PITR planning; does not change that flow.
 */
export function WalStatusPanel() {
  const { t } = useI18n();
  const notify = useNotify();
  const { formatDateTimeWithSeconds } = useDateFormatter();
  const query = useWalStatus(true);
  const seenError = useRef<unknown>(null);

  useEffect(() => {
    if (!query.isError) {
      seenError.current = null;
      return;
    }
    if (seenError.current === query.error) return;
    seenError.current = query.error;
    notify.error(
      translateApiError(t, query.error, {
        logContext: 'WalStatusPanel.load',
        fallbackKey: 'backupDr.walStatus.loadFailed',
      })
    );
  }, [notify, query.error, query.isError, t]);

  const status = query.data;

  if (query.isLoading && !status) {
    return (
      <Card data-testid="wal-status-panel" title={t('backupDr.walStatus.title')}>
        <Spin />
      </Card>
    );
  }

  if (!status) {
    return (
      <Card data-testid="wal-status-panel" title={t('backupDr.walStatus.title')}>
        <Alert type="error" showIcon title={t('backupDr.walStatus.loadFailed')} />
      </Card>
    );
  }

  const segmentAt = status.lastSuccessfulSegmentAtUtc;
  const tone = healthTone(status.healthLevel);

  return (
    <Card
      data-testid="wal-status-panel"
      data-tone={tone}
      title={t('backupDr.walStatus.title')}
      extra={<HealthBadge status={status} />}
    >
      {segmentAt ? (
        <Row gutter={[16, 16]}>
          <Col xs={24} md={8}>
            <Card data-testid="wal-status-last-segment" size="small">
              <Typography.Text type="secondary">{t('backupDr.walStatus.lastSegment')}</Typography.Text>
              <Typography.Title level={5} style={{ marginTop: 4, marginBottom: 0 }}>
                {status.lastSuccessfulSegmentName ?? '—'}
              </Typography.Title>
              <Typography.Text type="secondary">
                {t('backupDr.walStatus.segmentWhen', {
                  when: formatDateTimeWithSeconds(segmentAt),
                  age: dayjs(segmentAt).fromNow(),
                })}
              </Typography.Text>
            </Card>
          </Col>
          <Col xs={24} md={8}>
            <Card data-testid="wal-status-lag" size="small">
              <Typography.Text type="secondary">{t('backupDr.walStatus.lag')}</Typography.Text>
              <Typography.Title level={5} style={{ marginTop: 4, marginBottom: 0 }}>
                {status.lagSeconds == null
                  ? '—'
                  : t('backupDr.walStatus.lagValue', { seconds: status.lagSeconds })}
              </Typography.Title>
            </Card>
          </Col>
          <Col xs={24} md={8}>
            <Card data-testid="wal-status-segments" size="small">
              <Typography.Text type="secondary">{t('backupDr.walStatus.segments24h')}</Typography.Text>
              <Typography.Title level={5} style={{ marginTop: 4, marginBottom: 0 }}>
                {t('backupDr.walStatus.segmentsValue', {
                  archived: status.archivedCountLast24h,
                  failed: status.failedCountLast24h,
                })}
              </Typography.Title>
            </Card>
          </Col>
        </Row>
      ) : (
        <Alert
          data-testid="wal-status-empty"
          type="warning"
          showIcon
          title={t('backupDr.walStatus.empty')}
        />
      )}
    </Card>
  );
}
