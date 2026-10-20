'use client';

import { Card, Col, Empty, Progress, Row, Statistic, Timeline, Typography } from 'antd';
import React from 'react';

import type { DrillTimelineItem } from '@/features/backup/logic/backupDrInsights';
import { useI18n } from '@/i18n';

function formatDuration(ms: number | null): string | null {
  if (ms == null || !Number.isFinite(ms) || ms < 0) return null;
  const totalSec = Math.round(ms / 1000);
  const minutes = Math.floor(totalSec / 60);
  const seconds = totalSec % 60;
  if (minutes <= 0) return `${seconds}s`;
  return `${minutes}m ${seconds}s`;
}

export type RestoreDrillVisualizationProps = {
  items: DrillTimelineItem[];
  successRate: number | null;
  lastSuccessAt: string | null;
  daysSince: number | null;
  nextDrillAt: string | null;
  overdue: boolean;
  /** Accepted scheduled-drill gap. The last-success timestamp is unchanged. */
  overdueAccepted?: boolean;
  acceptedNote?: string | null;
  cadenceDays: number;
};

export function RestoreDrillVisualization({
  items,
  successRate,
  lastSuccessAt,
  daysSince,
  nextDrillAt,
  overdue,
  overdueAccepted = false,
  acceptedNote = null,
  cadenceDays,
}: RestoreDrillVisualizationProps) {
  const { t } = useI18n();
  const scheduleStatus = !overdue ? 'green' : overdueAccepted ? 'amber' : 'red';
  const resultKey = (result: DrillTimelineItem['result']) => {
    if (result === 'Succeeded') return 'backupDr.drills.result.succeeded';
    if (result === 'Failed') return 'backupDr.drills.result.failed';
    return 'backupDr.drills.result.pending';
  };

  return (
    <div data-testid="backup-drill-visualization">
      <Row gutter={[16, 16]}>
        <Col xs={24} md={8}>
          <Card size="small">
            <Typography.Text type="secondary">{t('backupDr.drills.successRate')}</Typography.Text>
            <Progress
              type="dashboard"
              percent={successRate ?? 0}
              format={() => (successRate == null ? '—' : `${successRate}%`)}
            />
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card size="small">
            <Statistic title={t('backupDr.drills.lastSuccess')} value={lastSuccessAt ?? '—'} />
            <Statistic
              title={t('backupDr.drills.daysSince')}
              value={daysSince ?? '—'}
              style={{ marginTop: 12 }}
            />
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card
            size="small"
            title={t('backupDr.drills.scheduleTitle')}
            data-testid="backup-drill-schedule"
            data-status={scheduleStatus}
          >
            {nextDrillAt ? (
              <Typography.Paragraph style={{ marginBottom: 8 }}>
                {overdue
                  ? t('backupDr.drills.scheduleOverdue', { date: nextDrillAt })
                  : t('backupDr.drills.scheduleNext', { date: nextDrillAt })}
              </Typography.Paragraph>
            ) : (
              <Typography.Paragraph style={{ marginBottom: 8 }}>
                {t('backupDr.drills.scheduleEmpty')}
              </Typography.Paragraph>
            )}
            {overdue && overdueAccepted && acceptedNote ? (
              <Typography.Paragraph data-testid="backup-drill-accepted-note" style={{ marginBottom: 8 }}>
                {acceptedNote}
              </Typography.Paragraph>
            ) : null}
            <Typography.Text type="secondary">
              {t('backupDr.drills.cadenceNote', { days: cadenceDays })}
            </Typography.Text>
          </Card>
        </Col>
      </Row>

      <Card size="small" title={t('backupDr.drills.timelineTitle')} style={{ marginTop: 16 }}>
        {items.length === 0 ? (
          <Empty description={t('backupDr.drills.empty')} />
        ) : (
            <Timeline
            items={items.map((item) => ({
              key: item.id,
              color: item.result === 'Succeeded' ? 'green' : item.result === 'Failed' ? 'red' : 'blue',
              content: (
                <div data-testid={`backup-drill-${item.id}`}>
                  <div>
                    {t('backupDr.drills.columns.date')}: {item.at}
                  </div>
                  <div>
                    {t('backupDr.drills.columns.duration')}:{' '}
                    {formatDuration(item.durationMs) ?? t('backupDr.drills.durationUnknown')}
                  </div>
                  <div>
                    {t('backupDr.drills.columns.result')}: {t(resultKey(item.result))}
                  </div>
                  <div>
                    {t('backupDr.drills.columns.reason')}:{' '}
                    {item.reason === '***' || !item.reason
                      ? item.reason === '***'
                        ? t('backupDr.drills.reasonHidden')
                        : '—'
                      : item.reason}
                  </div>
                </div>
              ),
            }))}
          />
        )}
      </Card>
    </div>
  );
}
