'use client';

import { useGetApiAuditLog } from '@/api/generated/audit-log/audit-log';
import { useGetApiAdminRestoreVerificationRuns } from '@/api/generated/admin-restore-verification/admin-restore-verification';
import { RestoreVerificationStatus } from '@/api/generated/model';
import { useI18n } from '@/i18n';
import { Card, List, Tag, Typography } from 'antd';
import { useCallback, useEffect, useState } from 'react';

const POLL_MS = 30_000;

type ProbeState = 'unknown' | 'up' | 'down';

function apiOrigin(): string {
  const configured = process.env.NEXT_PUBLIC_API_BASE_URL?.trim().replace(/\/$/, '');
  return configured && configured.length > 0 ? configured : 'http://localhost:5184';
}

async function probe(path: string): Promise<ProbeState> {
  try {
    const response = await fetch(`${apiOrigin()}${path}`, { cache: 'no-store' });
    return response.ok ? 'up' : 'down';
  } catch {
    return 'down';
  }
}

function statusLabel(status: number | undefined): string {
  if (status === RestoreVerificationStatus.Succeeded) return 'Succeeded';
  if (status === RestoreVerificationStatus.Failed) return 'Failed';
  if (status === RestoreVerificationStatus.Running) return 'Running';
  if (status === RestoreVerificationStatus.Queued) return 'Queued';
  return '—';
}

function ProbeTag({ state, label }: { state: ProbeState; label: string }) {
  if (state === 'up') return <Tag color="success">{label}</Tag>;
  if (state === 'down') return <Tag color="error">{label}</Tag>;
  return <Tag>{label}</Tag>;
}

/**
 * API liveness, the latest restore drills, and the last weekly drill-report audit row.
 * Reuses `/api/health/*`, `/api/admin/restore-verification/runs`, and `/api/AuditLog`.
 */
export function MonitoringApiHealthCard() {
  const { t } = useI18n();
  const [live, setLive] = useState<ProbeState>('unknown');
  const [ready, setReady] = useState<ProbeState>('unknown');
  const dsn = process.env.NEXT_PUBLIC_SENTRY_DSN?.trim() ?? '';
  const sentryEnvironment = process.env.NEXT_PUBLIC_SENTRY_ENVIRONMENT?.trim() || '—';

  const refresh = useCallback(async () => {
    const [liveState, readyState] = await Promise.all([
      probe('/api/health/live'),
      probe('/api/health/ready'),
    ]);
    setLive(liveState);
    setReady(readyState);
  }, []);

  useEffect(() => {
    void refresh();
    const timer = window.setInterval(() => void refresh(), POLL_MS);
    return () => window.clearInterval(timer);
  }, [refresh]);

  const drills = useGetApiAdminRestoreVerificationRuns(
    { page: 1, pageSize: 5 },
    { query: { refetchInterval: POLL_MS, retry: false } }
  );
  const report = useGetApiAuditLog(
    {
      action: 'BACKUP_DRILL_REPORT_SENT',
      page: 1,
      pageSize: 1,
      includeTotalCount: false,
    },
    { query: { refetchInterval: POLL_MS, retry: false } }
  );

  const drillItems = drills.data?.items?.slice(0, 5) ?? [];
  const lastReportAt = report.data?.auditLogs?.[0]?.createdAt;

  return (
    <Card size="small" title={t('monitoring.apiHealthTitle')} data-testid="api-health-card" style={{ marginTop: 16 }}>
      <Typography.Paragraph style={{ marginBottom: 8 }}>
        <span data-testid="api-health-live">
          <ProbeTag state={live} label={t('monitoring.apiLive')} />
        </span>{' '}
        <span data-testid="api-health-ready">
          <ProbeTag state={ready} label={t('monitoring.apiReady')} />
        </span>
      </Typography.Paragraph>

      <Typography.Text strong>{t('monitoring.recentDrills')}</Typography.Text>
      <div data-testid="restore-drills">
        <List
          size="small"
          locale={{ emptyText: t('monitoring.noDrills') }}
          dataSource={drillItems}
          rowKey={(item) => item.id ?? item.requestedAt ?? statusLabel(item.status)}
          renderItem={(item) => (
            <List.Item>
              {statusLabel(item.status)} · {item.requestedAt ?? '—'}
            </List.Item>
          )}
        />
      </div>

      <Typography.Paragraph style={{ marginTop: 8, marginBottom: 8 }} data-testid="drill-report-sent">
        {t('monitoring.lastDrillReport')}: {lastReportAt ?? '—'}
      </Typography.Paragraph>

      {dsn.length > 0 ? (
        <Typography.Text data-testid="sentry-environment">
          {t('monitoring.sentryEnvironment', { env: sentryEnvironment })}
        </Typography.Text>
      ) : (
        <Typography.Text type="warning" data-testid="sentry-dsn-missing">
          {t('monitoring.sentryDsnMissing')}
        </Typography.Text>
      )}
    </Card>
  );
}
