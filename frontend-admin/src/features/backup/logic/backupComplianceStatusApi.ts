/**
 * Backup compliance-status API — GET /api/admin/backup/compliance-status
 */
import { customInstance } from '@/lib/axios';

export const BACKUP_COMPLIANCE_STATUS_PATH = '/api/admin/backup/compliance-status' as const;

export function getBackupComplianceStatusQueryKey() {
  return [BACKUP_COMPLIANCE_STATUS_PATH] as const;
}

export type BackupComplianceListItemDto = {
  backupRunId: string;
  date: string;
  tenantId?: string | null;
  tenantName?: string | null;
  strategy?: string | number;
  status: string;
  compliant: boolean;
  reason: string;
};

export type BackupComplianceStatusResponseDto = {
  total: number;
  compliant: number;
  nonCompliant: number;
  allCompliant: boolean;
  lastCheckUtc: string;
  disclaimer?: string;
  restoreRequestsTotal?: number;
  restoreRequestsCompleted?: number;
  restoreRequestsFailed?: number;
  backups: BackupComplianceListItemDto[];
};

export async function getBackupComplianceStatus(): Promise<BackupComplianceStatusResponseDto> {
  return customInstance<BackupComplianceStatusResponseDto>({
    url: BACKUP_COMPLIANCE_STATUS_PATH,
    method: 'GET',
  });
}
