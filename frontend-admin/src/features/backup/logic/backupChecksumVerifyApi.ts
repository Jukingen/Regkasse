import { customInstance } from '@/lib/axios';

export type BackupChecksumArtifactStatus =
  | 'passed'
  | 'failed'
  | 'missing_hash'
  | 'missing_file'
  | string;

export interface BackupChecksumArtifactResult {
  artifactType: string;
  storedChecksum: string | null;
  computedChecksum: string | null;
  status: BackupChecksumArtifactStatus;
  detail: string | null;
}

export interface BackupChecksumVerifyResponse {
  runId: string;
  isValid: boolean;
  verifiedAtUtc: string;
  verifierSource: string;
  verificationId: string | null;
  failureReason: string | null;
  artifacts: BackupChecksumArtifactResult[];
}

export interface BackupManualVerifyTableRow {
  schemaName?: string;
  tableName: string;
  rowCount: number;
  presentInLogicalDump?: boolean;
  isVerified?: boolean;
  verificationMessage?: string | null;
}

export interface BackupManualVerifyTableReport {
  backupRunId?: string;
  status?: string;
  verificationScore?: number;
  tableStatistics?: BackupManualVerifyTableRow[];
}

export interface BackupManualVerifyResponse {
  backupId: string;
  isValid: boolean;
  verifiedAtUtc: string;
  verifierSource: string;
  verificationId: string | null;
  failureReason: string | null;
  artifacts: BackupChecksumArtifactResult[];
  tableReport?: BackupManualVerifyTableReport | null;
}

export function getBackupVerifyChecksumQueryKey(runId: string) {
  return ['/api/admin/backup/runs', runId, 'verify-checksum'] as const;
}

export function getBackupManualVerifyPath(backupId: string) {
  return `/api/admin/backup/${backupId}/verify`;
}

/** GET /api/admin/backup/runs/{id}/verify-checksum — on-demand SHA-256 re-hash (legacy). */
export async function verifyBackupChecksum(runId: string): Promise<BackupChecksumVerifyResponse> {
  return customInstance<BackupChecksumVerifyResponse>({
    url: `/api/admin/backup/runs/${runId}/verify-checksum`,
    method: 'GET',
  });
}

/** POST /api/admin/backup/{backupId}/verify — same SHA-256 as automatic verify + table TOC. */
export async function verifyBackup(backupId: string): Promise<BackupManualVerifyResponse> {
  return customInstance<BackupManualVerifyResponse>({
    url: getBackupManualVerifyPath(backupId),
    method: 'POST',
  });
}
