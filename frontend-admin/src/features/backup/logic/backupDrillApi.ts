/**
 * POST /api/admin/backup/drill/run — alias for restore-verification trigger.
 */
import { customInstance } from '@/lib/axios';

export const BACKUP_DRILL_RUN_PATH = '/api/admin/backup/drill/run' as const;

export interface RunRestoreDrillRequest {
  backupRunId?: string | null;
  idempotencyKey?: string | null;
}

export interface RestoreDrillResultDto {
  runId: string;
  success: boolean;
  status: string;
  completedAt: string;
  errors?: string[] | null;
  sourceBackupRunId?: string | null;
  newQueuedRunCreated?: boolean;
  existingRunReturned?: boolean;
  orchestrationState?: string;
  aliasOf?: string;
}

export async function runRestoreDrill(
  body?: RunRestoreDrillRequest
): Promise<RestoreDrillResultDto> {
  return customInstance<RestoreDrillResultDto>({
    url: BACKUP_DRILL_RUN_PATH,
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    data: body ?? {},
  });
}
