'use client';

import { ClockCircleOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import { Button, Space, Tooltip } from 'antd';
import React, { useCallback, useState } from 'react';

import { ManualRestoreRequestsTable } from '@/features/backup-dr/components/ManualRestoreRequestsTable';
import { RestoreApprovalModal } from '@/features/backup-dr/components/RestoreApprovalModal';
import { manualRestoreErrorMessage } from '@/features/backup-dr/logic/manualRestoreErrorMessage';
import {
  PitrRestoreModal,
  type PitrRestorePayload,
} from '@/features/backup/components/PitrRestoreModal';
import {
  PitrRestoreApprovalError,
  triggerPitrRestoreWithApproval,
} from '@/features/backup/logic/pitrRestoreApproval';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

export interface PitrRestoreWorkflowProps {
  canRestore: boolean;
  /** Show pending-approval table (Super Admin restore section). */
  showRequestsTable?: boolean;
  formatDt: (iso: string | undefined | null, formatLocale: string) => string;
  formatLocale: string;
}

export function PitrRestoreWorkflow({
  canRestore,
  showRequestsTable = false,
  formatDt,
  formatLocale,
}: PitrRestoreWorkflowProps) {
  const { message } = useAntdApp();

  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [pitrModalOpen, setPitrModalOpen] = useState(false);
  const [approvalRequestId, setApprovalRequestId] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const handleRestore = useCallback(
    async (payload: PitrRestorePayload) => {
      setSubmitting(true);
      try {
        await triggerPitrRestoreWithApproval(payload);
        setPitrModalOpen(false);
        message.success(t('backupDr.manualRestore.messages.requestCreated'));
        void queryClient.invalidateQueries({ queryKey: ['/api/admin/restore'] });
      } catch (err) {
        if (err instanceof PitrRestoreApprovalError) {
          if (err.code === 'MISSING_BASE_BACKUP') {
            message.error(t('backupDr.pitr.errors.missingBaseBackup'));
            return;
          }
          message.error(t('backupDr.pitr.validationFailedGeneric'));
          return;
        }
        message.error(manualRestoreErrorMessage(err, t));
      } finally {
        setSubmitting(false);
      }
    },
    [queryClient, t]
  );

  if (!canRestore) {
    return null;
  }

  return (
    <Space orientation="vertical" size={16} style={{ width: '100%' }}>
      <Tooltip title={t('backupDr.pitr.openButtonTooltip')}>
        <Button
          type="primary"
          danger
          icon={<ClockCircleOutlined />}
          onClick={() => setPitrModalOpen(true)}
        >
          {t('backupDr.pitr.openButton')}
        </Button>
      </Tooltip>

      <PitrRestoreModal
        open={pitrModalOpen}
        onClose={() => setPitrModalOpen(false)}
        onRestore={handleRestore}
        restoreSubmitting={submitting}
      />

      {showRequestsTable ? (
        <ManualRestoreRequestsTable
          canApprove={canRestore}
          formatDt={formatDt}
          formatLocale={formatLocale}
          onApprove={setApprovalRequestId}
          t={t}
        />
      ) : null}

      {approvalRequestId ? (
        <RestoreApprovalModal
          open
          requestId={approvalRequestId}
          onApproved={() => {
            setApprovalRequestId(null);
            void queryClient.invalidateQueries({ queryKey: ['/api/admin/restore'] });
            void queryClient.invalidateQueries({ queryKey: ['/api/admin/restore-verification'] });
          }}
          onClose={() => setApprovalRequestId(null)}
          t={t}
        />
      ) : null}
    </Space>
  );
}
