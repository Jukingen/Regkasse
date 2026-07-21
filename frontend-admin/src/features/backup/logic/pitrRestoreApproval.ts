import {
  type RestoreRequestStatusDto,
  postManualRestoreRequest,
} from '@/features/backup-dr/logic/manualRestoreApi';
import { defaultValidationDatabaseName } from '@/features/backup-dr/logic/manualRestorePresentation';
import type { RestorePointValidationResult } from '@/features/backup/logic/backupPitrApi';

export class PitrRestoreApprovalError extends Error {
  constructor(
    public readonly code: 'MISSING_BASE_BACKUP' | 'INVALID_VALIDATION',
    message: string
  ) {
    super(message);
    this.name = 'PitrRestoreApprovalError';
  }
}

export function buildPitrRestoreReason(params: {
  targetTimeUtc: string;
  recoveryMethod: string | null | undefined;
  estimatedDataLossSeconds: number | null | undefined;
}): string {
  const parts = [
    `PITR targetTimeUtc=${params.targetTimeUtc}`,
    params.recoveryMethod ? `recoveryMethod=${params.recoveryMethod}` : null,
    params.estimatedDataLossSeconds != null
      ? `estimatedDataLossSeconds=${params.estimatedDataLossSeconds}`
      : null,
  ].filter(Boolean);
  return parts.join('; ');
}

/**
 * Queues validation-only restore using the PITR base backup — second Super Admin approval required.
 */
export async function triggerPitrRestoreWithApproval(params: {
  targetTime: Date;
  validation: RestorePointValidationResult;
}): Promise<RestoreRequestStatusDto> {
  if (!params.validation.isValid) {
    throw new PitrRestoreApprovalError('INVALID_VALIDATION', 'Restore point is not valid.');
  }

  const backupRunId = params.validation.baseBackupId?.trim();
  if (!backupRunId) {
    throw new PitrRestoreApprovalError(
      'MISSING_BASE_BACKUP',
      'No base backup run for this restore point.'
    );
  }

  return postManualRestoreRequest({
    backupRunId,
    targetDatabaseName: defaultValidationDatabaseName(params.targetTime),
    validationOnly: true,
    reason: buildPitrRestoreReason({
      targetTimeUtc: params.targetTime.toISOString(),
      recoveryMethod: params.validation.recoveryMethod,
      estimatedDataLossSeconds: params.validation.estimatedDataLossSeconds,
    }),
  });
}
