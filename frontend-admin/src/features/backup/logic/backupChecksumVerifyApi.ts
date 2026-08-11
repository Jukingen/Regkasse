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

export function getBackupVerifyChecksumQueryKey(runId: string) {
  return ['/api/admin/backup/runs', runId, 'verify-checksum'] as const;
}

/** GET /api/admin/backup/runs/{id}/verify-checksum — on-demand SHA-256 re-hash. */
export async function verifyBackupChecksum(runId: string): Promise<BackupChecksumVerifyResponse> {
  return customInstance<BackupChecksumVerifyResponse>({
    url: `/api/admin/backup/runs/${runId}/verify-checksum`,
    method: 'GET',
  });
}
