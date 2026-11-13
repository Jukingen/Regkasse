'use client';

/**
 * Super Admin: create validation-only manual restore request.
 * Thin adapter over {@link RestoreModal} for BackupRunResponseDto callers.
 */
import React from 'react';

import type { BackupRunResponseDto } from '@/api/generated/model';
import type { RestoreRequestStatusDto } from '@/features/backup-dr/logic/manualRestoreApi';
import { RestoreModal, type RestoreModalProps } from '@/features/backup/components/RestoreModal';

export interface RestoreRequestModalProps {
  backupRun: BackupRunResponseDto;
  open: boolean;
  onClose: () => void;
  onRequestCreated?: (result: RestoreRequestStatusDto) => void;
  /** @deprecated RestoreModal uses useI18n; kept for call-site compatibility. */
  t?: (key: string, options?: Record<string, string | number>) => string;
}

export function RestoreRequestModal({
  backupRun,
  open,
  onClose,
  onRequestCreated,
}: RestoreRequestModalProps) {
  const props: RestoreModalProps = {
    open,
    onClose,
    onRequestCreated,
    backup: {
      backupRunId: backupRun.id ?? '',
      fileName: backupRun.id ?? null,
      tenantSlug: null,
    },
  };

  return <RestoreModal {...props} />;
}
