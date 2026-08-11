import { customInstance } from '@/lib/axios';

export type BackupContentValidationStatus =
  | 'Passed'
  | 'Failed'
  | 'Partial'
  | 'Unavailable'
  | 'passed'
  | 'failed'
  | 'partial'
  | 'unavailable'
  | 'warning'
  | 'skipped'
  | string;

export interface BackupContentTableValidation {
  tableKey: string;
  tableName?: string;
  manifestCount: number | null;
  liveCount: number | null;
  actualCount?: number | null;
  match?: boolean;
  status: BackupContentValidationStatus;
  detail: string | null;
}

export interface BackupContentFiscalCheck {
  checkName: string;
  passed: boolean;
  details: string | null;
}

export interface BackupContentFiscalValidation {
  status: BackupContentValidationStatus;
  paymentsInManifest: number | null;
  receiptsInManifest: number | null;
  liveSignedPayments: number | null;
  liveUnsignedPayments: number | null;
  chainBreakCount?: number | null;
  sequenceGapCount?: number | null;
  duplicateReceiptCount?: number | null;
  detail: string | null;
}

export interface BackupContentValidationDto {
  runId: string;
  validatedAtUtc: string;
  verificationId?: string | null;
  status?: BackupContentValidationStatus;
  overallStatus: BackupContentValidationStatus;
  summary: string | null;
  strategy: string;
  tables: BackupContentTableValidation[];
  fiscalChecks?: BackupContentFiscalCheck[];
  fiscal: BackupContentFiscalValidation | null;
  warnings: string[];
}

export function normalizeContentValidationStatus(
  status: string | null | undefined
): 'passed' | 'failed' | 'partial' | 'unavailable' | 'other' {
  const s = (status ?? '').trim().toLowerCase();
  if (s === 'passed') return 'passed';
  if (s === 'failed') return 'failed';
  if (s === 'partial' || s === 'warning') return 'partial';
  if (s === 'unavailable') return 'unavailable';
  return 'other';
}

export function getBackupContentValidationQueryKey(runId: string) {
  return ['/api/admin/backup/runs', runId, 'content-validation'] as const;
}

export async function getBackupContentValidation(
  runId: string
): Promise<BackupContentValidationDto> {
  return customInstance<BackupContentValidationDto>({
    url: `/api/admin/backup/runs/${runId}/content-validation`,
    method: 'GET',
  });
}
