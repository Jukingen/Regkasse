'use client';

/**
 * Super Admin acknowledgement of Backup/DR known gaps for the selected mandant.
 * Does not add a route. Acceptance does not turn a red gate green.
 */
import { useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Checkbox, Space, Spin, Typography } from 'antd';
import { useMemo, useState } from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';
import {
  backupKnownGapLabel,
  backupKnownGapsQueryKey,
  putBackupKnownGaps,
} from '@/features/backup/logic/backupKnownGapsApi';
import { useBackupKnownGaps } from '@/features/backup/hooks/useBackupKnownGaps';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { getHttpStatusFromError } from '@/lib/queryErrorHandling';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';
import { useTenantProviderValue } from '@/features/tenancy/providers/TenantProvider';

function readInvalidGapIds(error: unknown): string[] {
  if (!error || typeof error !== 'object') return [];
  const data = (error as { response?: { data?: unknown } }).response?.data;
  if (!data || typeof data !== 'object') return [];
  const raw = (data as { invalidGapIds?: unknown }).invalidGapIds;
  if (!Array.isArray(raw)) return [];
  return raw.filter((id): id is string => typeof id === 'string' && id.length > 0);
}

export function BackupKnownGapAcceptancePanel() {
  const { t } = useI18n();
  const { user } = useAuth();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const tenant = useTenantProviderValue();
  const tenantId = tenant?.tenantId ?? null;
  const canSave = hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL);
  const [checked, setChecked] = useState<string[]>([]);
  const [invalidIds, setInvalidIds] = useState<string[]>([]);
  const [appliedSnapshot, setAppliedSnapshot] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const query = useBackupKnownGaps(canSave ? tenantId : null);
  const gaps = useMemo(() => query.data?.gaps ?? [], [query.data]);
  const acceptedSignature = (query.data?.acceptance.acceptedGaps ?? []).join('\u0001');
  const snapshotKey = query.isSuccess
    ? `${acceptedSignature}|${gaps.map((gap) => gap.id).join(',')}`
    : null;

  if (canSave && query.isSuccess && snapshotKey != null && appliedSnapshot !== snapshotKey) {
    const knownIds = new Set(gaps.map((gap) => gap.id));
    setAppliedSnapshot(snapshotKey);
    setChecked(
      (query.data?.acceptance.acceptedGaps ?? []).filter((id) => knownIds.has(id))
    );
  }

  if (!canSave) return null;

  if (!tenantId) {
    return (
      <Alert
        type="info"
        showIcon
        style={{ marginTop: 16 }}
        title={t('backupDr.knownGapAcceptance.noTenant')}
      />
    );
  }

  const checkedSet = new Set(checked);
  const toggle = (id: string, next: boolean) => {
    setChecked((current) => {
      if (next) return current.includes(id) ? current : [...current, id];
      return current.filter((item) => item !== id);
    });
    setInvalidIds((current) => current.filter((item) => item !== id));
  };

  const save = async () => {
    const acceptedBy = user?.userName?.trim() || user?.email?.trim() || user?.id || '';
    if (!acceptedBy) {
      notify.error(t('backupDr.knownGapAcceptance.invalidGaps'));
      return;
    }
    const acceptedGapIds = gaps.map((gap) => gap.id).filter((id) => checkedSet.has(id));
    setSaving(true);
    try {
      await putBackupKnownGaps({ tenantId, acceptedGapIds, acceptedBy });
      setInvalidIds([]);
      notify.success(t('backupDr.knownGapAcceptance.saved'));
      await queryClient.invalidateQueries({ queryKey: backupKnownGapsQueryKey(tenantId) });
    } catch (error: unknown) {
      if (getHttpStatusFromError(error) === 400) {
        setInvalidIds(readInvalidGapIds(error));
        notify.error(t('backupDr.knownGapAcceptance.invalidGaps'));
        return;
      }
      notify.apiError(error, {
        logContext: 'BackupKnownGapAcceptancePanel.save',
        fallbackKey: 'backupDr.knownGapAcceptance.loadFailed',
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card
      data-testid="backup-known-gap-acceptance-panel"
      title={t('backupDr.knownGapAcceptance.title')}
      style={{ marginTop: 16 }}
    >
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('backupDr.knownGapAcceptance.description')}
        </Typography.Paragraph>
        {query.isLoading ? <Spin /> : null}
        {query.isError ? (
          <Alert type="error" showIcon title={t('backupDr.knownGapAcceptance.loadFailed')} />
        ) : null}
        {invalidIds.length > 0 ? (
          <Alert
            type="error"
            showIcon
            title={t('backupDr.knownGapAcceptance.invalidGaps')}
            description={invalidIds.join(', ')}
          />
        ) : null}
        {gaps.map((gap) => (
          <div key={gap.id} data-testid={`backup-known-gap-option-${gap.id}`}>
            <Checkbox
              checked={checkedSet.has(gap.id)}
              disabled={saving}
              onChange={(event) => toggle(gap.id, event.target.checked)}
            >
              {backupKnownGapLabel(t, gap.id, gap.title)}
            </Checkbox>
          </div>
        ))}
        <Button
          data-testid="backup-known-gap-save"
          type="primary"
          loading={saving}
          disabled={query.isLoading || query.isError}
          onClick={() => {
            void save();
          }}
        >
          {t('backupDr.knownGapAcceptance.save')}
        </Button>
      </Space>
    </Card>
  );
}
