'use client';

import { Button, Card, Descriptions, Table, Tag, Tooltip } from 'antd';
import React, { useMemo, useState } from 'react';

import { useGetApiAdminBackupRecoverabilitySummary } from '@/api/generated/admin-backup/admin-backup';
import { useBackupKnownGaps } from '@/features/backup/hooks/useBackupKnownGaps';
import { useRestoreVerificationRuns } from '@/features/backup/hooks/useRestoreVerificationRuns';
import {
  drillSchedulePreview,
  drillSuccessRateLast30Days,
  toDrillTimeline,
} from '@/features/backup/logic/backupDrInsights';
import { useTenantProviderValue } from '@/features/tenancy/providers/TenantProvider';
import { useI18n } from '@/i18n';
import { QUERY_STALE_VOLATILE_MS } from '@/lib/query/queryCachePolicy';

const SUMMARY_REFRESH_MS = 60_000;

function sameInstant(left: string | null | undefined, right: string | null | undefined): boolean {
  if (!left || !right) return false;
  const a = new Date(left).getTime();
  const b = new Date(right).getTime();
  return Number.isFinite(a) && Number.isFinite(b) && Math.abs(a - b) < 1000;
}

/** Live recoverability-summary plus a compare against the latest drills. No new route. */
export function RecoverabilityVerifySection() {
  const { t } = useI18n();
  const tenant = useTenantProviderValue();
  const [compare, setCompare] = useState(false);
  const summary = useGetApiAdminBackupRecoverabilitySummary({
    query: {
      staleTime: QUERY_STALE_VOLATILE_MS,
      refetchInterval: SUMMARY_REFRESH_MS,
    },
  });
  const drills = useRestoreVerificationRuns({ page: 1, pageSize: 5 }, compare);
  const known = useBackupKnownGaps(tenant?.tenantId ?? null);

  const proofAt = summary.data?.lastSuccessfulRestoreProofAt ?? null;
  const days =
    typeof summary.data?.restoreProofAgeSeconds === 'number'
      ? Math.floor(summary.data.restoreProofAgeSeconds / 86400)
      : null;
  const next = drillSchedulePreview(proofAt).nextAt;
  const items = useMemo(() => toDrillTimeline(drills.data?.items ?? []), [drills.data?.items]);
  const rate = drillSuccessRateLast30Days(items);
  const accepted = known.data?.acceptance.acceptedGaps.length ?? null;
  const lastPassed = items.find((item) => item.result === 'Succeeded') ?? null;
  const drift = compare && !sameInstant(proofAt, lastPassed?.at ?? null);

  return (
    <Card
      size="small"
      title={t('backupDr.recoverabilityCheck.title')}
      data-testid="recoverability-verify"
      style={{ marginTop: 16 }}
    >
      <Descriptions size="small" column={1}>
        <Descriptions.Item label={t('backupDr.recoverabilityCheck.lastProof')}>
          {proofAt ?? '—'}
        </Descriptions.Item>
        <Descriptions.Item label={t('backupDr.recoverabilityCheck.lastDrill')}>
          {proofAt ?? '—'}
        </Descriptions.Item>
        <Descriptions.Item label={t('backupDr.recoverabilityCheck.daysSince')}>
          {days ?? '—'}
        </Descriptions.Item>
        <Descriptions.Item label={t('backupDr.recoverabilityCheck.nextDrill')}>
          {next ?? '—'}
        </Descriptions.Item>
        <Descriptions.Item label={t('backupDr.recoverabilityCheck.successRate')}>
          {rate.rate == null ? '—' : `${rate.rate}%`}
        </Descriptions.Item>
        <Descriptions.Item label={t('backupDr.recoverabilityCheck.acceptedGaps')}>
          {accepted ?? '—'}
        </Descriptions.Item>
      </Descriptions>
      <Button style={{ marginTop: 12 }} onClick={() => setCompare(true)}>
        {t('backupDr.recoverabilityCheck.compare')}
      </Button>
      {compare ? (
        <Table
          style={{ marginTop: 12 }}
          size="small"
          pagination={false}
          rowKey="id"
          dataSource={items}
          columns={[
            { title: t('backupDr.drills.columns.date'), dataIndex: 'at' },
            { title: t('backupDr.drills.columns.result'), dataIndex: 'result' },
            {
              title: t('backupDr.recoverabilityCheck.drift'),
              render: (_, row) =>
                row.result === 'Succeeded' && drift ? (
                  <Tooltip title={t('backupDr.recoverabilityCheck.driftTooltip')}>
                    <Tag color="gold" data-testid="recoverability-drift">
                      {t('backupDr.recoverabilityCheck.drift')}
                    </Tag>
                  </Tooltip>
                ) : (
                  '—'
                ),
            },
          ]}
        />
      ) : null}
    </Card>
  );
}
