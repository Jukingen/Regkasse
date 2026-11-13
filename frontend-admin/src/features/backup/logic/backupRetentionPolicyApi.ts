/**
 * Backup retention policy API — GET/PUT /api/admin/backup/retention-policy
 */
import { customInstance } from '@/lib/axios';

import type { BackupStorageCostResponseDto } from '@/features/backup/logic/backupStorageCostsApi';

export const BACKUP_RETENTION_POLICY_PATH = '/api/admin/backup/retention-policy' as const;

export function getBackupRetentionPolicyQueryKey() {
  return [BACKUP_RETENTION_POLICY_PATH] as const;
}

export type BackupRetentionPolicyDto = {
  hotRetentionDays: number;
  warmRetentionDays: number;
  coldRetentionYears: number;
  coldStorageEnabled: boolean;
  legalRetentionEnforced: boolean;
  cloudProvider: string;
  cloudConfigured: boolean;
  cloudFallbackToFilesystem: boolean;
  legalBasis: string;
  systemLegalRetentionDays: number;
  updatedAtUtc: string;
  updatedByUserId?: string | null;
  storageCosts?: BackupStorageCostResponseDto | null;
};

export type BackupRetentionPolicyPutRequest = {
  hotRetentionDays: number;
  warmRetentionDays: number;
  coldRetentionYears: number;
  coldStorageEnabled: boolean;
  legalRetentionEnforced: boolean;
};

export type BackupMoveToColdResponse = {
  runId: string;
  success: boolean;
  artifactsArchived: number;
  artifactsDeduplicated: number;
  provider?: string | null;
  message?: string | null;
};

export async function getBackupRetentionPolicy(): Promise<BackupRetentionPolicyDto> {
  return customInstance<BackupRetentionPolicyDto>({
    url: BACKUP_RETENTION_POLICY_PATH,
    method: 'GET',
  });
}

export async function putBackupRetentionPolicy(
  body: BackupRetentionPolicyPutRequest
): Promise<BackupRetentionPolicyDto> {
  return customInstance<BackupRetentionPolicyDto>({
    url: BACKUP_RETENTION_POLICY_PATH,
    method: 'PUT',
    data: body,
  });
}

export async function moveBackupRunToCold(runId: string): Promise<BackupMoveToColdResponse> {
  return customInstance<BackupMoveToColdResponse>({
    url: `/api/admin/backup/${runId}/move-to-cold`,
    method: 'POST',
  });
}
