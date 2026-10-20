'use client';

/**
 * Amber note for accepted open Backup/DR gaps.
 * Does not read or replace lastSuccessfulRestoreProofAt.
 */
import { Alert, Space } from 'antd';

import { useBackupKnownGaps } from '@/features/backup/hooks/useBackupKnownGaps';
import { backupKnownGapLabel } from '@/features/backup/logic/backupKnownGapsApi';
import { useTenantProviderValue } from '@/features/tenancy/providers/TenantProvider';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/lib/dateUtils';

export function BackupKnownGapSummary() {
  const { t } = useI18n();
  const tenant = useTenantProviderValue();
  const known = useBackupKnownGaps(tenant?.tenantId ?? null);
  const acceptedOpen = (known.data?.gaps ?? []).filter((gap) => gap.open && gap.accepted);
  if (acceptedOpen.length === 0) return null;

  return (
    <Space orientation="vertical" size={8} style={{ width: '100%' }} data-testid="backup-known-gap-summary">
      {acceptedOpen.map((gap) => (
        <Alert
          key={gap.id}
          type="warning"
          showIcon
          data-testid={`backup-known-gap-summary-${gap.id}`}
          data-status="amber"
          title={backupKnownGapLabel(t, gap.id, gap.title)}
          description={t('backupDr.knownGapAcceptance.acceptedOn', {
            date: gap.acceptedAtUtc ? formatDateTime(gap.acceptedAtUtc) : '—',
          })}
        />
      ))}
    </Space>
  );
}
