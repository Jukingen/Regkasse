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

function isRecord(v: unknown): v is Record<string, unknown> {
  return v != null && typeof v === 'object' && !Array.isArray(v);
}

/** Normalize / validate the API payload so shape mismatches surface as query errors. */
export function normalizeBackupComplianceStatusResponse(
  raw: unknown
): BackupComplianceStatusResponseDto {
  if (!isRecord(raw)) {
    throw new Error('Invalid compliance-status response: expected an object');
  }

  const backupsRaw = raw.backups;
  if (backupsRaw != null && !Array.isArray(backupsRaw)) {
    throw new Error('Invalid compliance-status response: backups must be an array');
  }

  const toInt = (v: unknown, fallback = 0) =>
    typeof v === 'number' && Number.isFinite(v) ? v : fallback;

  return {
    total: toInt(raw.total),
    compliant: toInt(raw.compliant),
    nonCompliant: toInt(raw.nonCompliant),
    allCompliant: Boolean(raw.allCompliant),
    lastCheckUtc: typeof raw.lastCheckUtc === 'string' ? raw.lastCheckUtc : '',
    disclaimer: typeof raw.disclaimer === 'string' ? raw.disclaimer : undefined,
    restoreRequestsTotal: toInt(raw.restoreRequestsTotal),
    restoreRequestsCompleted: toInt(raw.restoreRequestsCompleted),
    restoreRequestsFailed: toInt(raw.restoreRequestsFailed),
    backups: (Array.isArray(backupsRaw) ? backupsRaw : []).map((item) => {
      const row = isRecord(item) ? item : {};
      return {
        backupRunId: String(row.backupRunId ?? ''),
        date: typeof row.date === 'string' ? row.date : '',
        tenantId: row.tenantId == null ? null : String(row.tenantId),
        tenantName: row.tenantName == null ? null : String(row.tenantName),
        strategy: row.strategy as string | number | undefined,
        status: typeof row.status === 'string' ? row.status : String(row.status ?? ''),
        compliant: Boolean(row.compliant),
        reason: typeof row.reason === 'string' ? row.reason : String(row.reason ?? ''),
      };
    }),
  };
}

export async function getBackupComplianceStatus(): Promise<BackupComplianceStatusResponseDto> {
  const raw = await customInstance<unknown>({
    url: BACKUP_COMPLIANCE_STATUS_PATH,
    method: 'GET',
  });
  return normalizeBackupComplianceStatusResponse(raw);
}
