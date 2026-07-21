'use client';

import { useQueryClient } from '@tanstack/react-query';
import React, { useCallback } from 'react';

import {
  getGetApiAdminBackupRecoverabilitySummaryQueryKey,
  usePostApiAdminBackupTrigger,
} from '@/api/generated/admin-backup/admin-backup';
import {
  getGetApiAdminRestoreVerificationReadinessQueryKey,
  getGetApiAdminRestoreVerificationRunsLatestQueryKey,
  usePostApiAdminRestoreVerificationTrigger,
} from '@/api/generated/admin-restore-verification/admin-restore-verification';
import { RestoreVerificationTriggerOrchestrationState } from '@/api/generated/model';
import {
  ManualActionsPanel,
  type ManualActionsPanelProps,
} from '@/features/backup-dr/components/ManualActionsPanel';
import { triggerErrorMessageBackupDashboard } from '@/features/backup-dr/logic/backupManualTriggerMessaging';
import { describeBackupTriggerOutcome } from '@/features/backup-dr/logic/backupTriggerOutcome';
import { buildBackupTriggerRequestBody } from '@/features/backup/api/backupHooks';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useCurrentTenant } from '@/hooks/useCurrentTenant';

export interface BackupManualActionsPanelProps extends Omit<
  ManualActionsPanelProps,
  'backupTrigger' | 'restoreTrigger'
> {}

/** Manual enqueue controls: preserves legacy mutation invalidate + toast semantics. */
export function BackupManualActionsPanel(props: BackupManualActionsPanelProps) {
  const { message } = useAntdApp();

  const { t } = props;
  const queryClient = useQueryClient();
  const { tenantId } = useCurrentTenant();

  const invalidateReadiness = useCallback(async () => {
    await queryClient.invalidateQueries({
      queryKey: getGetApiAdminRestoreVerificationReadinessQueryKey(),
    });
  }, [queryClient]);

  const backupTrigger = usePostApiAdminBackupTrigger({
    mutation: {
      onSuccess: async (res) => {
        const fb = describeBackupTriggerOutcome(res);
        const suffix = res.orchestrationState?.trim()
          ? ` ${t('backupDr.messages.orchestrationStateSuffix', { state: res.orchestrationState })}`
          : '';
        const text = `${t(fb.messageKey)}${suffix}`;
        if (fb.level === 'success') message.success(text);
        else message.info(text);
        await queryClient.invalidateQueries({
          queryKey: ['/api/admin/backup'],
        });
        await queryClient.invalidateQueries({
          queryKey: getGetApiAdminBackupRecoverabilitySummaryQueryKey(),
        });
        await invalidateReadiness();
      },
      onError: (err) => message.error(triggerErrorMessageBackupDashboard(err, t)),
    },
  });

  const restoreTrigger = usePostApiAdminRestoreVerificationTrigger({
    mutation: {
      onSuccess: async (res) => {
        if (res.newQueuedRunCreated) {
          message.success(t('backupDr.messages.restoreDrillEnqueued'));
        } else if (res.existingRunReturned) {
          if (res.orchestrationState === RestoreVerificationTriggerOrchestrationState.NUMBER_1) {
            message.info(t('backupDr.messages.restoreDrillIdempotent'));
          } else {
            message.info(t('backupDr.messages.restoreDrillExistingActive'));
          }
        }
        await queryClient.invalidateQueries({
          queryKey: getGetApiAdminRestoreVerificationRunsLatestQueryKey(),
        });
        await queryClient.invalidateQueries({
          queryKey: ['/api/admin/restore-verification/runs'],
        });
        await queryClient.invalidateQueries({
          queryKey: getGetApiAdminBackupRecoverabilitySummaryQueryKey(),
        });
        await invalidateReadiness();
      },
      onError: (err) => message.error(triggerErrorMessageBackupDashboard(err, t)),
    },
  });

  return (
    <ManualActionsPanel
      {...props}
      backupTrigger={{
        isPending: backupTrigger.isPending,
        mutate: () => backupTrigger.mutate({ data: buildBackupTriggerRequestBody({}, tenantId) }),
      }}
      restoreTrigger={restoreTrigger as ManualActionsPanelProps['restoreTrigger']}
    />
  );
}
