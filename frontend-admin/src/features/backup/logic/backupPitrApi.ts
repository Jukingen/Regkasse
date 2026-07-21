import { customInstance } from '@/lib/axios';

const PITR_AVAILABILITY_PATH = '/api/admin/backup/pitr/availability';
const PITR_VALIDATE_PATH = '/api/admin/backup/pitr/validate';

export type PitrRecoveryMethod = 'PITR' | 'FullBackupOnly';

export interface PitrAvailabilityResponse {
  earliestRestorePointUtc: string | null;
  latestRestorePointUtc: string | null;
  supportedTimePointsUtc: string[];
  walArchivingEnabled: boolean;
  walArchiveLagMinutes: number | null;
  message: string | null;
}

export interface ValidatePitrRestorePointRequest {
  targetTimeUtc: string;
}

export interface RestorePointValidationResult {
  isValid: boolean;
  message: string | null;
  baseBackupId: string | null;
  baseBackupTimeUtc: string | null;
  targetTimeUtc: string | null;
  estimatedDataLossSeconds: number | null;
  recoveryMethod: PitrRecoveryMethod | null;
}

export async function getPitrAvailability(): Promise<PitrAvailabilityResponse> {
  return customInstance<PitrAvailabilityResponse>({
    url: PITR_AVAILABILITY_PATH,
    method: 'GET',
  });
}

export async function validatePitrRestorePoint(
  body: ValidatePitrRestorePointRequest
): Promise<RestorePointValidationResult> {
  return customInstance<RestorePointValidationResult>({
    url: PITR_VALIDATE_PATH,
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    data: body,
  });
}
